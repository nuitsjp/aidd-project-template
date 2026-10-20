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

    // 障害時動作: DB 書込み失敗で確定済みの内容と版が変わらないことを、失敗を注入して確認する。
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
