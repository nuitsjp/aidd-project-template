using Aidd.ReactDotnet.Application;
using Aidd.ReactDotnet.Application.Authentication;
using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Domain;
using Aidd.ReactDotnet.Features.Notes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class NotesServiceTests
{
    private static readonly Principal Alice = new("alice", "Alice");
    private static readonly Principal Bob = new("bob", "Bob");

    [TestMethod]
    public void TitleValidationAndNormalizationAreIndependent()
    {
        NoteRules.ValidateTitle("  対象  ");
        Assert.AreEqual("対象", NoteRules.NormalizeTitle("  対象  "));
        NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 100)));
        Assert.AreEqual("VALIDATION", TestAssert.Throws<AppFaultException>(() =>
            NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 101)))).Code);
        TestAssert.Throws<AppFaultException>(() => NoteRules.ValidateBody(string.Concat(Enumerable.Repeat("😀", 10_001))));
    }

    [TestMethod]
    public void PurePreviewRejectsDuplicatesAfterTrimming()
    {
        var preview = NotesService.Preview(new BulkInput(" 一件目 \r\n\r\n二件目", "本文"));
        CollectionAssert.AreEqual(new[] { "一件目", "二件目" }, preview.Titles.ToArray());
        var error = TestAssert.Throws<AppFaultException>(() =>
            NotesService.Preview(new BulkInput("同じ\n 同じ ", string.Empty)));
        Assert.AreEqual("VALIDATION", error.Code);
        Assert.AreEqual("入力内でタイトルが重複しています。", error.Message);
    }

    [TestMethod]
    public async Task CreateEditRemovePersistsAndDetectsStaleVersion()
    {
        using var fixture = new TestDatabase();
        var created = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "対象", "initial")));
        Assert.AreEqual(1L, created.Version);
        var updated = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(created.Id, created.Version, created.Title, "updated")));
        Assert.AreEqual(2L, updated.Version);
        Assert.AreEqual("updated", fixture.Notes.List("alice").Single().Body);
        Assert.IsInstanceOfType<SaveNote.SaveResult.Conflict>(await ExecuteAsync(fixture.Save.Presentation,
            Alice, new SaveNoteRequest(created.Id, created.Version, created.Title, "stale")));
        Assert.AreEqual("updated", fixture.Notes.Get("alice", created.Id).Body);
        fixture.Notes.Remove("alice", updated.Id, updated.Version);
        Assert.AreEqual(0, fixture.Notes.List("alice").Count);
    }

    [TestMethod]
    public async Task ConcurrentEditsSerializeAndOnlyOneVersionWins()
    {
        using var fixture = new TestDatabase();
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

        CollectionAssert.AreEquivalent(new[] { "saved", "EDIT_CONFLICT" }, results);
        Assert.AreEqual(2L, fixture.Notes.Get("alice", original.Id).Version);
    }

    [TestMethod]
    public async Task OwnerBoundaryAppliesToReadWriteAndDelete()
    {
        using var fixture = new TestDatabase();
        var alice = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "同じタイトル", "private")));
        Success(await ExecuteAsync(fixture.Save.Presentation, Bob, new SaveNoteRequest(null, null, "同じタイトル", "other")));
        Assert.AreEqual("NOT_FOUND", TestAssert.Throws<AppFaultException>(() => fixture.Notes.Get("bob", alice.Id)).Code);
        Assert.IsInstanceOfType<SaveNote.SaveResult.NotFound>(await ExecuteAsync(fixture.Save.Presentation,
            Bob, new SaveNoteRequest(alice.Id, alice.Version, alice.Title, "attack")));
        TestAssert.Throws<AppFaultException>(() => fixture.Notes.Remove("bob", alice.Id, alice.Version));
        Assert.AreEqual("private", fixture.Notes.Get("alice", alice.Id).Body);
        Assert.AreEqual("other", fixture.Notes.List("bob").Single().Body);
    }

    [TestMethod]
    public void ImportRollsBackEveryRowWhenLaterInsertFails()
    {
        using var fixture = new TestDatabase();
        var fixedId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        fixture.Notes = new NotesService(fixture.Database, fixture.Notifications)
        {
            NewId = () => fixedId,
        };
        TestAssert.Throws<SqliteException>(() =>
            fixture.Notes.ImportMany("alice", new BulkInput("一件目\n二件目", "本文")));
        Assert.AreEqual(0, fixture.Notes.List("alice").Count);
    }

    [TestMethod]
    public async Task InstanceIoDelegatesDoNotLeakToAnotherService()
    {
        using var fixture = new TestDatabase();
        var firstId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var firstApplication = new SaveNote.ApplicationLayer(
            fixture.Database, fixture.Notifications);
        firstApplication.NewId = () => firstId;
        firstApplication.UtcNow = () => DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var secondApplication = new SaveNote.ApplicationLayer(
            fixture.Database, fixture.Notifications);
        secondApplication.NewId = () => secondId;
        secondApplication.UtcNow = () => DateTimeOffset.Parse("2026-02-01T00:00:00Z");

        var a = Success(await ExecuteAsync(firstApplication, Alice, new SaveNoteRequest(null, null, "first", string.Empty)));
        var b = Success(await ExecuteAsync(secondApplication, Alice, new SaveNoteRequest(null, null, "second", string.Empty)));
        Assert.AreEqual(firstId.ToString("D"), a.Id);
        Assert.AreEqual(secondId.ToString("D"), b.Id);
        Assert.AreEqual("2026-01-01T00:00:00.0000000+00:00", a.UpdatedAt);
        Assert.AreEqual("2026-02-01T00:00:00.0000000+00:00", b.UpdatedAt);
    }

    [TestMethod]
    public async Task SaveCanReplaceItsResultIoAndNotifiesOnlyAfterPersistence()
    {
        var errors = new List<Exception>();
        var notifications = new ChangeNotifications(errors.Add);
        var persisted = false;
        var notified = false;
        var expectedId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var expectedTime = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        using var fixture = new TestDatabase();
        var application = new SaveNote.ApplicationLayer(
            fixture.Database, notifications);
        application.NewId = () => expectedId;
        application.UtcNow = () => expectedTime;
        application.Insert = (_, command) =>
        {
            Assert.AreEqual("正規化", command.Title);
            Assert.IsFalse(notified);
            persisted = true;
            return Task.FromResult(1);
        };
        application.Read = (_, ownerId, id) => Task.FromResult<Note?>(
            new Note(id, "正規化", "本文", 1, expectedTime.ToString("O")));
        application.PublishChange = ownerId =>
        {
            Assert.AreEqual("alice", ownerId);
            Assert.IsTrue(persisted);
            notified = true;
            return true;
        };

        var result = Success(await ExecuteAsync(application, Alice, new SaveNoteRequest(null, null, "  正規化  ", "本文")));
        Assert.AreEqual(expectedId.ToString("D"), result.Id);
        Assert.IsTrue(notified);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public async Task PresentationExecuteCanBeReplacedBeforeHttpConversion()
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

        Assert.AreEqual(StatusCodes.Status409Conflict, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [TestMethod]
    public async Task SaveValidationFailureDoesNotRunIo()
    {
        var errors = new List<Exception>();
        var notifications = new ChangeNotifications(errors.Add);
        var generated = false;
        var clockRead = false;
        var persisted = false;
        var notified = false;
        var application = new SaveNote.ApplicationLayer(
            new Database("unused.sqlite"), notifications);
        application.NewId = () =>
        {
            generated = true;
            return Guid.Empty;
        };
        application.UtcNow = () =>
        {
            clockRead = true;
            return DateTimeOffset.UtcNow;
        };
        application.Read = (_, _, _) =>
        {
            persisted = true;
            return Task.FromResult<Note?>(null);
        };
        application.Insert = (_, _) =>
        {
            persisted = true;
            return Task.FromResult(0);
        };
        application.Update = (_, _) =>
        {
            persisted = true;
            return Task.FromResult(0);
        };
        application.PublishChange = _ =>
        {
            notified = true;
            return true;
        };

        var isValid = application.TryValidate(
            new SaveNoteRequest(null, null, "  ", "本文"), out var validationErrors);

        Assert.IsFalse(isValid);
        Assert.AreEqual("タイトルは1〜100文字で入力してください。", validationErrors!["title"].Single());
        Assert.IsFalse(generated);
        Assert.IsFalse(clockRead);
        Assert.IsFalse(persisted);
        Assert.IsFalse(notified);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void RequestAnnotationsReportFieldErrorsWithoutChangingInput()
    {
        var application = new SaveNote.ApplicationLayer(
            new Database("unused.sqlite"), new ChangeNotifications(_ => { }));
        var request = new SaveNoteRequest("invalid-id", 0, "  ", new string('x', 10_001));

        Assert.IsFalse(application.TryValidate(request, out var errors));
        CollectionAssert.AreEquivalent(new[] { "title", "body", "id", "version" }, errors!.Keys.ToArray());
        Assert.AreEqual("  ", request.Title);

        var missingVersion = new SaveNoteRequest("00000000-0000-4000-8000-000000000001", null, "対象", "");
        Assert.IsFalse(application.TryValidate(missingVersion, out errors));
        Assert.AreEqual("編集対象と版を指定してください。", errors![string.Empty].Single());
    }

    [TestMethod]
    public async Task NotificationRunsAfterCommitAndFailureDoesNotUndoSave()
    {
        using var fixture = new TestDatabase();
        var observedCount = -1;
        using var successful = fixture.Notifications.Subscribe("alice", () => observedCount = fixture.Notes.List("alice").Count);
        using var failing = fixture.Notifications.Subscribe("alice", () => throw new InvalidOperationException("notification failed"));
        var note = Success(await ExecuteAsync(fixture.Save.Presentation, Alice, new SaveNoteRequest(null, null, "committed", string.Empty)));
        Assert.AreEqual(1, observedCount);
        Assert.AreEqual(1, fixture.NotificationErrors.Count);
        Assert.AreEqual(1L, fixture.Notes.Get("alice", note.Id).Version);
    }

    private static async Task<SaveNote.SaveResult> ExecuteAsync(
        SaveNote.PresentationLayer presentation,
        Principal principal,
        SaveNoteRequest request)
    {
        Assert.IsTrue(presentation.TryValidate(request, out var errors), Format(errors));
        return await presentation.ExecuteAsync(principal, request);
    }

    private static async Task<SaveNote.SaveResult> ExecuteAsync(
        IApplicationLayer<SaveNoteRequest, SaveNote.SaveResult> application,
        Principal principal,
        SaveNoteRequest request)
    {
        Assert.IsTrue(application.TryValidate(request, out var errors), Format(errors));
        return await application.ExecuteAsync(principal, request);
    }

    private static SaveNoteResponse Success(SaveNote.SaveResult result) =>
        result is SaveNote.SaveResult.Success success
            ? success.Response
            : throw new AssertFailedException($"保存成功ではなく {result.GetType().Name} が返されました。");

    private static string Format(IReadOnlyDictionary<string, string[]>? errors) =>
        errors is null ? string.Empty : string.Join("; ", errors.SelectMany(item => item.Value));
}
