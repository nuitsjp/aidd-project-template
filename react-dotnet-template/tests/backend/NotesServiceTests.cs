using Aidd.ReactDotnet.Features.Notes;
using Aidd.ReactDotnet.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class NotesServiceTests
{
    [TestMethod]
    public void PureValidationUsesUnicodeCodePointsAndNormalizesTitles()
    {
        Assert.AreEqual("対象", NotesService.ValidateTitle("  対象  "));
        Assert.AreEqual(100, NotesService.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 100))).EnumerateRunes().Count());
        Assert.AreEqual("VALIDATION", TestAssert.Throws<AppFaultException>(() =>
            NotesService.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 101)))).Code);
        TestAssert.Throws<AppFaultException>(() => NotesService.ValidateBody(string.Concat(Enumerable.Repeat("😀", 10_001))));
    }

    [TestMethod]
    public void PurePreviewRejectsDuplicatesAfterTrimming()
    {
        var preview = NotesService.Preview(new BulkInput(" 一件目 \r\n\r\n二件目", "本文"));
        CollectionAssert.AreEqual(new[] { "一件目", "二件目" }, preview.Titles.ToArray());
        var error = TestAssert.Throws<AppFaultException>(() =>
            NotesService.Preview(new BulkInput("同じ\n 同じ ", string.Empty)));
        Assert.AreEqual("VALIDATION", error.Code);
        Assert.IsTrue(error.FieldErrors?.ContainsKey("titles"));
    }

    [TestMethod]
    public void CreateEditRemovePersistsAndDetectsStaleVersion()
    {
        using var fixture = new TestDatabase();
        var created = fixture.Notes.Save("alice", new SaveNote(null, null, "対象", "initial"));
        Assert.AreEqual(1L, created.Version);
        var updated = fixture.Notes.Save("alice", new SaveNote(created.Id, created.Version, created.Title, "updated"));
        Assert.AreEqual(2L, updated.Version);
        Assert.AreEqual("updated", fixture.Notes.List("alice").Single().Body);
        Assert.AreEqual("EDIT_CONFLICT", TestAssert.Throws<AppFaultException>(() =>
            fixture.Notes.Save("alice", new SaveNote(created.Id, created.Version, created.Title, "stale"))).Code);
        Assert.AreEqual("updated", fixture.Notes.Get("alice", created.Id).Body);
        fixture.Notes.Remove("alice", updated.Id, updated.Version);
        Assert.AreEqual(0, fixture.Notes.List("alice").Count);
    }

    [TestMethod]
    public async Task ConcurrentEditsSerializeAndOnlyOneVersionWins()
    {
        using var fixture = new TestDatabase();
        var original = fixture.Notes.Save("alice", new SaveNote(null, null, "parallel", "v1"));
        using var start = new ManualResetEventSlim(false);
        var attempts = new[] { "first", "second" }.Select(body => Task.Run(() =>
        {
            start.Wait();
            try
            {
                fixture.Notes.Save("alice", new SaveNote(original.Id, original.Version, original.Title, body));
                return "saved";
            }
            catch (AppFaultException error)
            {
                return error.Code;
            }
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);

        CollectionAssert.AreEquivalent(new[] { "saved", "EDIT_CONFLICT" }, results);
        Assert.AreEqual(2L, fixture.Notes.Get("alice", original.Id).Version);
    }

    [TestMethod]
    public void OwnerBoundaryAppliesToReadWriteAndDelete()
    {
        using var fixture = new TestDatabase();
        var alice = fixture.Notes.Save("alice", new SaveNote(null, null, "同じタイトル", "private"));
        fixture.Notes.Save("bob", new SaveNote(null, null, "同じタイトル", "other"));
        Assert.AreEqual("NOT_FOUND", TestAssert.Throws<AppFaultException>(() => fixture.Notes.Get("bob", alice.Id)).Code);
        TestAssert.Throws<AppFaultException>(() =>
            fixture.Notes.Save("bob", new SaveNote(alice.Id, alice.Version, alice.Title, "attack")));
        TestAssert.Throws<AppFaultException>(() => fixture.Notes.Remove("bob", alice.Id, alice.Version));
        Assert.AreEqual("private", fixture.Notes.Get("alice", alice.Id).Body);
        Assert.AreEqual("other", fixture.Notes.List("bob").Single().Body);
    }

    [TestMethod]
    public void ImportRollsBackEveryRowWhenLaterInsertFails()
    {
        using var fixture = new TestDatabase();
        var fixedId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        fixture.Notes = new NotesService(fixture.Path, fixture.Notifications, fixture.NotificationErrors.Add)
        {
            NewId = () => fixedId,
        };
        TestAssert.Throws<SqliteException>(() =>
            fixture.Notes.ImportMany("alice", new BulkInput("一件目\n二件目", "本文")));
        Assert.AreEqual(0, fixture.Notes.List("alice").Count);
    }

    [TestMethod]
    public void InstanceIoDelegatesDoNotLeakToAnotherService()
    {
        using var fixture = new TestDatabase();
        var firstId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var first = new NotesService(fixture.Path, fixture.Notifications, fixture.NotificationErrors.Add)
        {
            NewId = () => firstId,
            UtcNow = () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        };
        var second = new NotesService(fixture.Path, fixture.Notifications, fixture.NotificationErrors.Add)
        {
            NewId = () => secondId,
            UtcNow = () => DateTimeOffset.Parse("2026-02-01T00:00:00Z"),
        };

        var a = first.Save("alice", new SaveNote(null, null, "first", string.Empty));
        var b = second.Save("alice", new SaveNote(null, null, "second", string.Empty));
        Assert.AreEqual(firstId.ToString("D"), a.Id);
        Assert.AreEqual(secondId.ToString("D"), b.Id);
        Assert.AreEqual("2026-01-01T00:00:00.0000000+00:00", a.UpdatedAt);
        Assert.AreEqual("2026-02-01T00:00:00.0000000+00:00", b.UpdatedAt);
    }

    [TestMethod]
    public void SaveCanReplaceItsResultIoAndNotifiesOnlyAfterPersistence()
    {
        var errors = new List<Exception>();
        var notifications = new ChangeNotifications(errors.Add);
        var persisted = false;
        var notified = false;
        var expectedId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var expectedTime = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        var service = new NotesService("unused.sqlite", notifications, errors.Add)
        {
            NewId = () => expectedId,
            UtcNow = () => expectedTime,
            PersistSave = command =>
            {
                Assert.AreEqual("正規化", command.Title);
                Assert.IsFalse(notified);
                persisted = true;
                return new Note(command.Id, command.Title, command.Body, 1, command.UpdatedAt.ToString("O"));
            },
            PublishChange = ownerId =>
            {
                Assert.AreEqual("alice", ownerId);
                Assert.IsTrue(persisted);
                notified = true;
                return true;
            },
        };

        var result = service.Save("alice", new SaveNote(null, null, "  正規化  ", "本文"));
        Assert.AreEqual(expectedId.ToString("D"), result.Id);
        Assert.IsTrue(notified);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void NotificationRunsAfterCommitAndFailureDoesNotUndoSave()
    {
        using var fixture = new TestDatabase();
        var observedCount = -1;
        using var successful = fixture.Notifications.Subscribe("alice", () => observedCount = fixture.Notes.List("alice").Count);
        using var failing = fixture.Notifications.Subscribe("alice", () => throw new InvalidOperationException("notification failed"));
        var note = fixture.Notes.Save("alice", new SaveNote(null, null, "committed", string.Empty));
        Assert.AreEqual(1, observedCount);
        Assert.AreEqual(1, fixture.NotificationErrors.Count);
        Assert.AreEqual(1L, fixture.Notes.Get("alice", note.Id).Version);
    }
}
