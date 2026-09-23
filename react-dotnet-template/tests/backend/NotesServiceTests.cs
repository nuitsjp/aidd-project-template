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
using System.ComponentModel.DataAnnotations;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class NotesServiceTests
{
    private static readonly Principal Alice = new("alice", "Alice");
    private static readonly Principal Bob = new("bob", "Bob");

    [TestMethod]
    public void TitleAndBodyValidationUseRuneCounts()
    {
        NoteRules.ValidateTitle("  対象  ");
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
    public async Task CreateEditRemovePersistsAndDetectsStaleVersionAsync()
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
    public async Task ConcurrentEditsSerializeAndOnlyOneVersionWinsAsync()
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
    public async Task OwnerBoundaryAppliesToReadWriteAndDeleteAsync()
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
    public async Task SaveCanReplaceItsResultIoAndNotifiesOnlyAfterPersistenceAsync()
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
        application.InsertAsync = (_, ownerId, title, body) =>
        {
            Assert.AreEqual("alice", ownerId);
            Assert.AreEqual("正規化", title);
            Assert.AreEqual("本文", body);
            Assert.IsFalse(notified);
            persisted = true;
            return Task.FromResult(new Note(expectedId.ToString("D"), title, body, 1, expectedTime.ToString("O")));
        };
        application.ReadAsync = (_, _, _) => throw new AssertFailedException("新規保存後の読み直しは不要です。");
        application.PublishChange = ownerId =>
        {
            Assert.AreEqual("alice", ownerId);
            Assert.IsTrue(persisted);
            notified = true;
        };

        var result = Success(await ExecuteAsync(application, Alice, new SaveNoteRequest(null, null, "  正規化  ", "本文")));
        Assert.AreEqual(expectedId.ToString("D"), result.Id);
        Assert.IsTrue(notified);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
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

        Assert.AreEqual(StatusCodes.Status409Conflict, ((IStatusCodeHttpResult)result).StatusCode);
    }

    [TestMethod]
    public void RequestAnnotationsReportFieldErrorsWithoutChangingInput()
    {
        var request = new SaveNoteRequest("invalid-id", 0, "  ", new string('x', 10_001));
        var errors = new List<ValidationResult>();
        Assert.IsFalse(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        CollectionAssert.AreEquivalent(new[] { "Title", "Body", "Id", "Version" },
            errors.SelectMany(error => error.MemberNames).ToArray());
        Assert.AreEqual("  ", request.Title);

        var missingVersion = new SaveNoteRequest("00000000-0000-4000-8000-000000000001", null, "対象", "");
        errors.Clear();
        Assert.IsFalse(Validator.TryValidateObject(missingVersion, new ValidationContext(missingVersion), errors, true));
        Assert.AreEqual("編集対象と版を指定してください。", errors.Single().ErrorMessage);
        CollectionAssert.AreEquivalent(new[] { "Id", "Version" }, errors.Single().MemberNames.ToArray());
    }

    [TestMethod]
    public async Task NotificationRunsAfterCommitAndFailureDoesNotUndoSaveAsync()
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
        return await presentation.ExecuteAsync(principal, request);
    }

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
            : throw new AssertFailedException($"保存成功ではなく {result.GetType().Name} が返されました。");

}
