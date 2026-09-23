using NotesSample.Infrastructure.Persistence;
using NotesSample.Infrastructure.Configuration;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotesSample.Tests;

[TestClass]
public sealed class DatabaseAndConfigTests
{
    [TestMethod]
    public async Task DatabaseInstancesKeepPathsIndependentAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-database-instances-{Guid.NewGuid():N}");
        try
        {
            var first = new Database(Path.Combine(directory, "first.sqlite"));
            var second = new Database(Path.Combine(directory, "second.sqlite"));
            await first.InitializeAsync();
            await second.InitializeAsync();

            await using (var connection = await first.OpenAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO users
                    VALUES
                        ('first', 'First');
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await using var firstConnection = await first.OpenAsync();
            await using var secondConnection = await second.OpenAsync();
            Assert.AreEqual(1L, await ScalarAsync<long>(firstConnection, """
                SELECT COUNT(*)
                FROM
                    users
                """));
            Assert.AreEqual(0L, await ScalarAsync<long>(secondConnection, """
                SELECT COUNT(*)
                FROM
                    users
                """));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [TestMethod]
    public async Task DatabaseEnablesMigrationWalAndForeignKeysAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        await using var connection = await fixture.Database.OpenAsync();
        Assert.AreEqual(1L, await ScalarAsync<long>(connection, "PRAGMA user_version"));
        Assert.AreEqual("wal", await ScalarAsync<string>(connection, "PRAGMA journal_mode"));
        Assert.AreEqual(1L, await ScalarAsync<long>(connection, "PRAGMA foreign_keys"));
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO notes
            VALUES
                ('n', 'missing', 'a', '', 1, 'time')
            """;
        await TestAssert.ThrowsAsync<SqliteException>(async () => await command.ExecuteNonQueryAsync());
    }

    [TestMethod]
    public async Task TransactionExposesConnectionAndSupportsCommitRollbackAndModeAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();

        await using (var transaction = await fixture.Database.BeginTransactionAsync())
        {
            await transaction.Connection.ExecuteAsync("""
                INSERT INTO users
                VALUES
                    ('committed', 'Committed');
                """);
            await transaction.CommitAsync();
        }

        await using (var transaction = await fixture.Database.BeginTransactionAsync(TransactionMode.Deferred))
        {
            await transaction.Connection.ExecuteAsync("""
                INSERT INTO users
                VALUES
                    ('rolled-back', 'Rolled Back');
                """);
            await transaction.RollbackAsync();
        }

        await using var connection = await fixture.Database.OpenAsync();
        Assert.AreEqual(1L, await ScalarAsync<long>(connection, """
            SELECT COUNT(*)
            FROM
                users
            WHERE
                id = 'committed'
            """));
        Assert.AreEqual(0L, await ScalarAsync<long>(connection, """
            SELECT COUNT(*)
            FROM
                users
            WHERE
                id = 'rolled-back'
            """));
    }

    [TestMethod]
    public async Task ReopenPreservesDataAndUnknownSchemaIsRejectedAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-version-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "app.sqlite");
        try
        {
            var database = new Database(path);
            await database.InitializeAsync();
            await using (var connection = await database.OpenAsync())
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO users
                    VALUES
                        ('id', 'name');
                    PRAGMA user_version = 99;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await TestAssert.ThrowsAsync<InvalidOperationException>(() => database.InitializeAsync());
            await using var readonlyConnection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            await readonlyConnection.OpenAsync();
            Assert.AreEqual("name", await ScalarAsync<string>(readonlyConnection, """
                SELECT name
                FROM
                    users
                WHERE
                    id = 'id'
                """));
            Assert.AreEqual(99L, await ScalarAsync<long>(readonlyConnection, "PRAGMA user_version"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [TestMethod]
    public async Task BackupCopiesCommittedWalStateAndPassesQuickCheckAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        var request = new NotesSample.Features.Notes.SaveNoteRequest(null, null, "backup", "copy");
        await fixture.Save.Presentation.ExecuteAsync(
            new NotesSample.Application.Authentication.Principal("alice", "Alice"), request);
        var backup = Path.Combine(Path.GetDirectoryName(fixture.Path)!, "backup.sqlite");
        fixture.Database.Backup(backup);
        var check = await new Database(backup).CheckAsync();
        Assert.IsTrue(check.IsHealthy);
        await using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        Assert.AreEqual("copy", await ScalarAsync<string>(connection, """
            SELECT body
            FROM
                notes
            """));
    }

    [TestMethod]
    public void ConfigAcceptsDynamicPortAndRejectsUnsafeExposure()
    {
        var values = new Dictionary<string, string?>
        {
            ["HOST"] = "127.0.0.1",
            ["PORT"] = "0",
            ["DB_PATH"] = Path.Combine(Path.GetTempPath(), "config-test.sqlite"),
            ["PUBLIC_ORIGIN"] = string.Empty,
        };
        var config = AppConfig.FromValues(name => values.GetValueOrDefault(name));
        Assert.AreEqual(0, config.Port);
        Assert.IsNull(config.PublicOrigin);

        values["HOST"] = "0.0.0.0";
        TestAssert.Throws<InvalidOperationException>(() => AppConfig.FromValues(name => values.GetValueOrDefault(name)));
        values["HOST"] = "127.0.0.1";
        values["AUTH_MODE"] = "proxy";
        TestAssert.Throws<InvalidOperationException>(() => AppConfig.FromValues(name => values.GetValueOrDefault(name)));
        values["PUBLIC_ORIGIN"] = "https://example.com";
        Assert.AreEqual("proxy", AppConfig.FromValues(name => values.GetValueOrDefault(name)).AuthMode);
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }
}
