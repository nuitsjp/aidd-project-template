using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Domain;
using Aidd.ReactDotnet.Features.Notes;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class NotesServiceTests
{
    [TestMethod]
    public void PureValidationUsesUnicodeCodePointsAndNormalizesTitles()
    {
        Assert.AreEqual("対象", NoteRules.ValidateTitle("  対象  "));
        Assert.AreEqual(100, NoteRules.ValidateTitle(string.Concat(Enumerable.Repeat("😀", 100))).EnumerateRunes().Count());
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
        Assert.IsTrue(error.FieldErrors?.ContainsKey("titles"));
    }

    [TestMethod]
    public void CreateEditRemovePersistsAndDetectsStaleVersion()
    {
        using var fixture = new TestDatabase();
        var created = fixture.Save.Execute("alice", new SaveNote.Input(null, null, "対象", "initial"));
        Assert.AreEqual(1L, created.Version);
        var updated = fixture.Save.Execute("alice", new SaveNote.Input(created.Id, created.Version, created.Title, "updated"));
        Assert.AreEqual(2L, updated.Version);
        Assert.AreEqual("updated", fixture.Notes.List("alice").Single().Body);
        Assert.AreEqual("EDIT_CONFLICT", TestAssert.Throws<AppFaultException>(() =>
            fixture.Save.Execute("alice", new SaveNote.Input(created.Id, created.Version, created.Title, "stale"))).Code);
        Assert.AreEqual("updated", fixture.Notes.Get("alice", created.Id).Body);
        fixture.Notes.Remove("alice", updated.Id, updated.Version);
        Assert.AreEqual(0, fixture.Notes.List("alice").Count);
    }

    [TestMethod]
    public async Task ConcurrentEditsSerializeAndOnlyOneVersionWins()
    {
        using var fixture = new TestDatabase();
        var original = fixture.Save.Execute("alice", new SaveNote.Input(null, null, "parallel", "v1"));
        using var start = new ManualResetEventSlim(false);
        var attempts = new[] { "first", "second" }.Select(body => Task.Run(() =>
        {
            start.Wait();
            try
            {
                fixture.Save.Execute("alice", new SaveNote.Input(original.Id, original.Version, original.Title, body));
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
        var alice = fixture.Save.Execute("alice", new SaveNote.Input(null, null, "同じタイトル", "private"));
        fixture.Save.Execute("bob", new SaveNote.Input(null, null, "同じタイトル", "other"));
        Assert.AreEqual("NOT_FOUND", TestAssert.Throws<AppFaultException>(() => fixture.Notes.Get("bob", alice.Id)).Code);
        TestAssert.Throws<AppFaultException>(() =>
            fixture.Save.Execute("bob", new SaveNote.Input(alice.Id, alice.Version, alice.Title, "attack")));
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
        var first = new SaveNote(fixture.Path, fixture.Notifications, fixture.NotificationErrors.Add)
        {
            NewId = () => firstId,
            UtcNow = () => DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        };
        var second = new SaveNote(fixture.Path, fixture.Notifications, fixture.NotificationErrors.Add)
        {
            NewId = () => secondId,
            UtcNow = () => DateTimeOffset.Parse("2026-02-01T00:00:00Z"),
        };

        var a = first.Execute("alice", new SaveNote.Input(null, null, "first", string.Empty));
        var b = second.Execute("alice", new SaveNote.Input(null, null, "second", string.Empty));
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
        var service = new SaveNote("unused.sqlite", notifications, errors.Add)
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

        var result = service.Execute("alice", new SaveNote.Input(null, null, "  正規化  ", "本文"));
        Assert.AreEqual(expectedId.ToString("D"), result.Id);
        Assert.IsTrue(notified);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void SaveValidationFailureDoesNotRunIo()
    {
        var errors = new List<Exception>();
        var notifications = new ChangeNotifications(errors.Add);
        var generated = false;
        var clockRead = false;
        var persisted = false;
        var notified = false;
        var service = new SaveNote("unused.sqlite", notifications, errors.Add)
        {
            NewId = () =>
            {
                generated = true;
                return Guid.Empty;
            },
            UtcNow = () =>
            {
                clockRead = true;
                return DateTimeOffset.UtcNow;
            },
            PersistSave = _ =>
            {
                persisted = true;
                return new Note(string.Empty, string.Empty, string.Empty, 0, string.Empty);
            },
            PublishChange = _ =>
            {
                notified = true;
                return true;
            },
        };

        var error = TestAssert.Throws<AppFaultException>(() =>
            service.Execute("alice", new SaveNote.Input(null, null, "  ", "本文")));

        Assert.AreEqual("VALIDATION", error.Code);
        Assert.IsFalse(generated);
        Assert.IsFalse(clockRead);
        Assert.IsFalse(persisted);
        Assert.IsFalse(notified);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void NotificationRunsAfterCommitAndFailureDoesNotUndoSave()
    {
        using var fixture = new TestDatabase();
        var observedCount = -1;
        using var successful = fixture.Notifications.Subscribe("alice", () => observedCount = fixture.Notes.List("alice").Count);
        using var failing = fixture.Notifications.Subscribe("alice", () => throw new InvalidOperationException("notification failed"));
        var note = fixture.Save.Execute("alice", new SaveNote.Input(null, null, "committed", string.Empty));
        Assert.AreEqual(1, observedCount);
        Assert.AreEqual(1, fixture.NotificationErrors.Count);
        Assert.AreEqual(1L, fixture.Notes.Get("alice", note.Id).Version);
    }
}
