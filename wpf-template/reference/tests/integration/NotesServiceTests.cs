using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Shouldly;
using WpfNotesSample.Model.Domain.Notes;
using WpfNotesSample.Model.Infrastructure.Sqlite;
using WpfNotesSample.Model.Infrastructure.Sqlite.Notes;
using Xunit;

namespace WpfNotesSample.IntegrationTests;

public sealed class NotesServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "WpfNotesSample-tests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;
    private readonly NotesService notes;

    public NotesServiceTests()
    {
        databasePath = Path.Combine(directory, "notes.sqlite3");
        var database = new Database(databasePath);
        database.Initialize();
        notes = new NotesService(database);
    }

    [Fact]
    public async Task SaveUpdateAndReloadPersistContentAndVersion()
    {
        var saved = await notes.SaveAsync(null, null, "  記録  ", "最初の本文");
        var updated = await notes.SaveAsync(saved.Id, saved.Version, "編集した記録", "日本語\n\U0001F427");
        var reopened = new Database(databasePath);
        reopened.Initialize();

        var loaded = (await new NotesService(reopened).ListAsync()).Single();

        saved.Title.ShouldBe("記録");
        saved.Version.ShouldBe(1);
        loaded.Id.ShouldBe(saved.Id);
        loaded.Title.ShouldBe("編集した記録");
        loaded.Body.ShouldBe("日本語\n\U0001F427");
        loaded.Version.ShouldBe(2);
        loaded.UpdatedAt.ShouldBe(updated.UpdatedAt);
        loaded.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task StaleUpdateLeavesTheNewerContentIntact()
    {
        var saved = await notes.SaveAsync(null, null, "元の内容", "");
        var winner = await notes.SaveAsync(saved.Id, saved.Version, "先に保存された内容", "新しい本文");

        await Should.ThrowAsync<NoteConflictException>(() => notes.SaveAsync(saved.Id, saved.Version, "古い画面の入力", "古い本文"));

        var loaded = (await notes.ListAsync()).Single();
        loaded.Title.ShouldBe(winner.Title);
        loaded.Body.ShouldBe(winner.Body);
        loaded.Version.ShouldBe(winner.Version);
    }

    [Fact]
    public async Task DeleteRequiresTheCurrentVersionAndRemovesTheNote()
    {
        var saved = await notes.SaveAsync(null, null, "削除対象", "");
        var updated = await notes.SaveAsync(saved.Id, saved.Version, "更新された削除対象", "");

        await Should.ThrowAsync<NoteConflictException>(() => notes.RemoveAsync(saved.Id, saved.Version));
        (await notes.ListAsync()).Count.ShouldBe(1);
        await notes.RemoveAsync(updated.Id, updated.Version);

        (await notes.ListAsync()).ShouldBeEmpty();
        await Should.ThrowAsync<NoteConflictException>(() => notes.RemoveAsync(updated.Id, updated.Version));
    }

    [Fact]
    public async Task ImportCommitsAllNotesAndAllowsTheSameTitle()
    {
        var imported = await notes.ImportAsync(new[]
        {
            new NoteInput("  同じタイトル  ", "1件目"),
            new NoteInput("同じタイトル", "2件目"),
        });

        var loaded = await notes.ListAsync();
        imported.Count.ShouldBe(2);
        loaded.Count.ShouldBe(2);
        loaded.Select(note => note.Id).Distinct().Count().ShouldBe(2);
        loaded.All(note => note.Title == "同じタイトル" && note.Version == 1).ShouldBeTrue();
        loaded.Select(note => note.Body).OrderBy(body => body).ShouldBe(new[] { "1件目", "2件目" });
    }

    [Fact]
    public async Task InvalidLaterInputLeavesTheDatabaseUnchanged()
    {
        var existing = await notes.SaveAsync(null, null, "既存", "保持する本文");

        await Should.ThrowAsync<NoteValidationException>(() => notes.ImportAsync(new[]
        {
            new NoteInput("先頭の正常なメモ", ""),
            new NoteInput("  ", ""),
        }));

        var loaded = (await notes.ListAsync()).Single();
        loaded.Id.ShouldBe(existing.Id);
        loaded.Body.ShouldBe(existing.Body);
    }

    [Fact]
    public async Task DatabaseFailureOnTheSecondInsertRollsBackTheFirstInsert()
    {
        var existing = await notes.SaveAsync(null, null, "既存", "保持する本文");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER fail_second_import
                BEFORE INSERT ON notes WHEN NEW.title = '拒否されるメモ'
                BEGIN
                    SELECT RAISE(ABORT, 'forced second insert failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var error = await Should.ThrowAsync<SqliteException>(() => notes.ImportAsync(new[]
        {
            new NoteInput("先頭の正常なメモ", ""),
            new NoteInput("拒否されるメモ", ""),
        }));

        error.Message.ShouldContain("forced second insert failure");
        var loaded = (await notes.ListAsync()).Single();
        loaded.Id.ShouldBe(existing.Id);
        loaded.Body.ShouldBe(existing.Body);
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }
}
