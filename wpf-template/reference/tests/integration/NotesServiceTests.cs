using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Shouldly;
using WpfNotesSample.Domain.Notes;
using WpfNotesSample.Infrastructure.Sqlite;
using WpfNotesSample.Infrastructure.Sqlite.Notes;
using Xunit;

namespace WpfNotesSample.IntegrationTests;

public sealed class NotesServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WpfNotesSample-tests", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
    private readonly string _databasePath;
    private readonly NotesService _notes;

    public NotesServiceTests()
    {
        _databasePath = Path.Combine(_directory, "notes.sqlite3");
        var database = new Database(_databasePath);
        database.Initialize();
        _notes = new NotesService(database);
    }

    [Fact]
    public async Task SaveUpdateAndReloadPersistContentAndVersion()
    {
        var saved = await _notes.SaveAsync(null, null, "  記録  ", "最初の本文");
        var updated = await _notes.SaveAsync(saved.Id, saved.Version, "編集した記録", "日本語\n\U0001F427");
        var reopened = new Database(_databasePath);
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
        var saved = await _notes.SaveAsync(null, null, "元の内容", "");
        var winner = await _notes.SaveAsync(saved.Id, saved.Version, "先に保存された内容", "新しい本文");

        await Should.ThrowAsync<NoteConflictException>(() => _notes.SaveAsync(saved.Id, saved.Version, "古い画面の入力", "古い本文"));

        var loaded = (await _notes.ListAsync()).Single();
        loaded.Title.ShouldBe(winner.Title);
        loaded.Body.ShouldBe(winner.Body);
        loaded.Version.ShouldBe(winner.Version);
    }

    [Fact]
    public async Task DeleteRequiresTheCurrentVersionAndRemovesTheNote()
    {
        var saved = await _notes.SaveAsync(null, null, "削除対象", "");
        var updated = await _notes.SaveAsync(saved.Id, saved.Version, "更新された削除対象", "");

        await Should.ThrowAsync<NoteConflictException>(() => _notes.RemoveAsync(saved.Id, saved.Version));
        (await _notes.ListAsync()).Count.ShouldBe(1);
        await _notes.RemoveAsync(updated.Id, updated.Version);

        (await _notes.ListAsync()).ShouldBeEmpty();
        await Should.ThrowAsync<NoteConflictException>(() => _notes.RemoveAsync(updated.Id, updated.Version));
    }

    [Fact]
    public async Task GetReturnsTheRequestedNoteWithItsSavedContentAndVersion()
    {
        var saved = await _notes.SaveAsync(null, null, "  開くノート  ", "本文\n\U0001F427");
        await _notes.SaveAsync(null, null, "別のノート", "別の本文");

        var loaded = await _notes.GetAsync(saved.Id);

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(saved.Id);
        loaded.Title.ShouldBe("開くノート");
        loaded.Body.ShouldBe("本文\n\U0001F427");
        loaded.Version.ShouldBe(1);
        loaded.UpdatedAt.ShouldBe(saved.UpdatedAt);
        loaded.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetReturnsNullForAnUnknownOrDeletedNote()
    {
        var saved = await _notes.SaveAsync(null, null, "削除するノート", "本文");

        (await _notes.GetAsync(Guid.NewGuid())).ShouldBeNull();
        await _notes.RemoveAsync(saved.Id, saved.Version);

        (await _notes.GetAsync(saved.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task GettingTheNoteAgainReadsAnUpdateSavedByAnotherService()
    {
        var saved = await _notes.SaveAsync(null, null, "更新前のノート", "更新前の本文");
        var firstRead = await _notes.GetAsync(saved.Id);
        firstRead.ShouldNotBeNull();
        firstRead.Version.ShouldBe(1);
        var otherDatabase = new Database(_databasePath);
        otherDatabase.Initialize();
        var updated = await new NotesService(otherDatabase).SaveAsync(saved.Id, saved.Version,
            "最新のノート", "別のサービスで保存した本文");

        var reloaded = await _notes.GetAsync(saved.Id);

        reloaded.ShouldNotBeNull();
        reloaded.Id.ShouldBe(saved.Id);
        reloaded.Title.ShouldBe(updated.Title);
        reloaded.Body.ShouldBe(updated.Body);
        reloaded.Version.ShouldBe(2);
        reloaded.UpdatedAt.ShouldBe(updated.UpdatedAt);
        firstRead.Title.ShouldBe("更新前のノート");
        firstRead.Version.ShouldBe(1);
    }

    [Fact]
    public async Task DatabaseFailureDuringSaveLeavesTheSavedContentAndVersionIntact()
    {
        var existing = await _notes.SaveAsync(null, null, "既存", "保持する本文");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
        }.ToString()))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER fail_note_update
                BEFORE UPDATE ON notes
                BEGIN
                    SELECT RAISE(ABORT, 'forced note update failure');
                END;
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var error = await Should.ThrowAsync<SqliteException>(() => _notes.SaveAsync(existing.Id, existing.Version,
            "保存に失敗するノート", "保存に失敗する本文"));

        error.Message.ShouldContain("forced note update failure");
        var loaded = (await _notes.ListAsync()).Single();
        loaded.Id.ShouldBe(existing.Id);
        loaded.Title.ShouldBe(existing.Title);
        loaded.Body.ShouldBe(existing.Body);
        loaded.Version.ShouldBe(existing.Version);
        loaded.UpdatedAt.ShouldBe(existing.UpdatedAt);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }
}
