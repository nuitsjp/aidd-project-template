using NotesSample.Application;
using NotesSample.Application.Authentication;
using NotesSample.Infrastructure.Persistence;
using NotesSample.Infrastructure.Notifications;
using NotesSample.Domain.Notes;
using NotesSample.Domain;
using NotesSample.Features.Notes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;
using Dapper;
using System.ComponentModel.DataAnnotations;

namespace NotesSample.Tests;

public sealed class NotesFeatureTests
{
    private static readonly Principal Alice = new("alice", "Alice");
    private static readonly Principal Bob = new("bob", "Bob");

    [Fact]
    public void TitleAndBodyValidationUseRuneCounts()
    {
        NoteRules.ValidateTitle("  対象  ");
        NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 100)));
        (Should.Throw<AppFaultException>(() =>
            NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 101)))).Code).ShouldBe("VALIDATION");
        Should.Throw<AppFaultException>(() => NoteRules.ValidateBody(string.Concat(Enumerable.Repeat("😀", 10_001))));
    }

    [Fact]
    public async Task PurePreviewRejectsDuplicatesAfterTrimmingAsync()
    {
        var service = new PreviewNotes();
        var preview = await service.Presentation.ExecuteAsync(
            Alice, new BulkInput(" 一件目 \r\n\r\n二件目", "本文"));
        preview.Titles.ShouldBe(new[] { "一件目", "二件目" });
        var error = await Should.ThrowAsync<AppFaultException>(async () =>
            await service.Presentation.ExecuteAsync(Alice, new BulkInput("同じ\n 同じ ", string.Empty)));
        (error.Code).ShouldBe("VALIDATION");
        (error.Message).ShouldBe("入力内でタイトルが重複しています。");
    }

    [Fact]
    public async Task CreateEditRemovePersistsAndDetectsStaleVersionAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        var created = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "対象", "initial")));
        (created.Version).ShouldBe(1L);
        var updated = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(created.Id, created.Version, created.Title, "updated")));
        (updated.Version).ShouldBe(2L);
        ((await ListAsync(fixture.ListNotes, Alice)).Single().Body).ShouldBe("updated");
        (await ExecuteAsync(fixture.Save.Presentation,
            Alice, new SaveNoteRequest(created.Id, created.Version, created.Title, "stale")))
            .ShouldBeOfType<SaveNote.SaveResult.Conflict>();
        ((await GetAsync(fixture.GetNote, Alice, created.Id)).Body).ShouldBe("updated");
        (await fixture.RemoveNote.Presentation.ExecuteApplicationAsync(
            Alice, new RemoveNoteInput(updated.Id, updated.Version)))
            .ShouldBeOfType<RemoveNote.RemoveResult.Success>();
        ((await ListAsync(fixture.ListNotes, Alice)).Count).ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentEditsSerializeAndOnlyOneVersionWinsAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        var original = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "parallel", "v1")));
        using var start = new ManualResetEventSlim(false);
        var attempts = new[] { "first", "second" }.Select(body => Task.Run(async () =>
        {
            start.Wait();
            var result = await ExecuteAsync(fixture.Save.Presentation,
                Alice, new SaveNoteRequest(original.Id, original.Version, original.Title, body));
            return result switch
            {
                SaveNote.SaveResult.Success => "saved",
                SaveNote.SaveResult.Conflict => "EDIT_CONFLICT",
                _ => "unexpected",
            };
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);

        results.Order().ShouldBe(new[] { "EDIT_CONFLICT", "saved" });
        ((await GetAsync(fixture.GetNote, Alice, original.Id)).Version).ShouldBe(2L);
    }

    [Fact]
    public async Task OwnerBoundaryAppliesToReadWriteAndDeleteAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        var alice = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "同じタイトル", "private")));
        Success(await ExecuteAsync(fixture.Save.Presentation, Bob, new SaveNoteRequest(null, null, "同じタイトル", "other")));
        ((await Should.ThrowAsync<AppFaultException>(
            () => GetAsync(fixture.GetNote, Bob, alice.Id))).Code).ShouldBe("NOT_FOUND");
        (await fixture.RemoveNote.Presentation.ExecuteApplicationAsync(
            Bob, new RemoveNoteInput(alice.Id, alice.Version)))
            .ShouldBeOfType<RemoveNote.RemoveResult.NotFound>();
        ((await GetAsync(fixture.GetNote, Alice, alice.Id)).Body).ShouldBe("private");
        ((await ListAsync(fixture.ListNotes, Bob)).Single().Body).ShouldBe("other");
    }

    [Fact]
    public async Task ImportRollsBackEveryRowWhenLaterInsertFailsAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        await fixture.ImportNotes.Presentation.ExecuteAsync(Alice, new BulkInput("二件目", "既存"));
        var error = await Should.ThrowAsync<AppFaultException>(() =>
            fixture.ImportNotes.Presentation.ExecuteAsync(Alice, new BulkInput("一件目\n二件目", "本文")));
        (error.Code).ShouldBe("TITLE_EXISTS");
        (await ListAsync(fixture.ListNotes, Alice)).Select(note => note.Title).ShouldBe(new[] { "二件目" });
    }

    [Fact]
    public async Task SaveCanReplaceItsResultIoAndNotifiesOnlyAfterPersistenceAsync()
    {
        var errors = new List<Exception>();
        var notifications = new ChangeNotifications(errors.Add);
        var persisted = false;
        var notified = false;
        var expectedId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var expectedTime = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        using var fixture = await TestDatabase.CreateAsync();
        var application = new SaveNote.ApplicationLayer(
            fixture.Database, notifications);
        application.InsertAsync = (_, ownerId, title, body) =>
        {
            (ownerId).ShouldBe("alice");
            (title).ShouldBe("正規化");
            (body).ShouldBe("本文");
            (notified).ShouldBeFalse();
            persisted = true;
            return Task.FromResult(new Note(expectedId.ToString("D"), title, body, 1, expectedTime.ToString("O")));
        };
        application.ReadAsync = (_, _, _) => throw new ShouldAssertException("新規保存後の読み直しは不要です。");
        application.PublishChange = ownerId =>
        {
            (ownerId).ShouldBe("alice");
            (persisted).ShouldBeTrue();
            notified = true;
        };

        var result = Success(await ExecuteAsync(application, Alice, new SaveNoteRequest(null, null, "  正規化  ", "本文")));
        (result.Id).ShouldBe(expectedId.ToString("D"));
        (notified).ShouldBeTrue();
        (errors.Count).ShouldBe(0);
    }

    [Fact]
    public async Task PresentationExecuteCanBeReplacedBeforeHttpConversionAsync()
    {
        var notifications = new ChangeNotifications(_ => { });
        var application = new SaveNote.ApplicationLayer(
            new Database("unused.sqlite"), notifications);
        var presentation = new SaveNote.PresentationLayer(application)
        {
            ExecuteAsync = (_, _) => Task.FromResult<SaveNote.SaveResult>(new SaveNote.SaveResult.Conflict()),
        };

        var result = await presentation.HandleAsync(
            Alice, new SaveNoteRequest(null, null, "対象", "本文"));

        (((IStatusCodeHttpResult)result).StatusCode).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void RequestAnnotationsReportFieldErrorsWithoutChangingInput()
    {
        var request = new SaveNoteRequest("invalid-id", 0, "  ", new string('x', 10_001));
        var errors = new List<ValidationResult>();
        (Validator.TryValidateObject(request, new ValidationContext(request), errors, true)).ShouldBeFalse();
        errors.SelectMany(error => error.MemberNames).Order().ShouldBe(new[] { "Body", "Id", "Title", "Version" });
        (request.Title).ShouldBe("  ");

        var missingVersion = new SaveNoteRequest("00000000-0000-4000-8000-000000000001", null, "対象", "");
        errors.Clear();
        (Validator.TryValidateObject(missingVersion, new ValidationContext(missingVersion), errors, true)).ShouldBeFalse();
        (errors.Single().ErrorMessage).ShouldBe("編集対象と版を指定してください。");
        errors.Single().MemberNames.Order().ShouldBe(new[] { "Id", "Version" });
    }

    [Fact]
    public async Task NotificationRunsAfterCommitAndFailureDoesNotUndoSaveAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        var observedCount = -1;
        using var successful = fixture.Notifications.Subscribe("alice", () =>
        {
            using var connection = new SqliteConnection($"Data Source={fixture.Path};Mode=ReadOnly;Pooling=False");
            connection.Open();
            observedCount = connection.ExecuteScalar<int>("""
                SELECT COUNT(*)
                FROM notes
                WHERE owner_id = @ownerId
                """, new { ownerId = "alice" });
        });
        using var failing = fixture.Notifications.Subscribe("alice", () => throw new InvalidOperationException("notification failed"));
        var note = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "committed", string.Empty)));
        (observedCount).ShouldBe(1);
        (fixture.NotificationErrors.Count).ShouldBe(1);
        ((await GetAsync(fixture.GetNote, Alice, note.Id)).Version).ShouldBe(1L);
    }

    private static async Task<SaveNote.SaveResult> ExecuteAsync(
        SaveNote.PresentationLayer presentation,
        Principal principal,
        SaveNoteRequest request)
    {
        return await presentation.ExecuteAsync(principal, request);
    }

    private static Task<IReadOnlyList<Note>> ListAsync(ListNotes notes, Principal principal) =>
        notes.Presentation.ExecuteAsync(principal);

    private static Task<Note> GetAsync(GetNote note, Principal principal, string id) =>
        note.Presentation.ExecuteAsync(principal, id);

    private static async Task<SaveNote.SaveResult> ExecuteAsync(
        IApplicationLayer<SaveNoteRequest, SaveNote.SaveResult> application,
        Principal principal,
        SaveNoteRequest request)
    {
        return await application.ExecuteAsync(principal, request);
    }

    private static SaveNoteResponse Success(SaveNote.SaveResult result) =>
        result is SaveNote.SaveResult.Success success
            ? success.Response
            : throw new ShouldAssertException($"保存成功ではなく {result.GetType().Name} が返されました。");

}
