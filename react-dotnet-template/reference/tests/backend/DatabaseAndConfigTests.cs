using NotesSample.Infrastructure.Persistence;
using NotesSample.Infrastructure.Configuration;
using Dapper;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace NotesSample.Tests;

public sealed class DatabaseAndConfigTests
{
    [Fact]
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
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await using var firstConnection = await first.OpenAsync();
            await using var secondConnection = await second.OpenAsync();
            (await ScalarAsync<long>(firstConnection, """
                SELECT COUNT(*)
                FROM
                    users
                """)).ShouldBe(1L);
            (await ScalarAsync<long>(secondConnection, """
                SELECT COUNT(*)
                FROM
                    users
                """)).ShouldBe(0L);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task DatabaseEnablesMigrationWalAndForeignKeysAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        await using var connection = await fixture.Database.OpenAsync();
        (await ScalarAsync<long>(connection, "PRAGMA user_version")).ShouldBe(1L);
        (await ScalarAsync<string>(connection, "PRAGMA journal_mode")).ShouldBe("wal");
        (await ScalarAsync<long>(connection, "PRAGMA foreign_keys")).ShouldBe(1L);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO notes
            VALUES
                ('n', 'missing', 'a', '', 1, 'time')
            """;
        await Should.ThrowAsync<SqliteException>(async () => await command.ExecuteNonQueryAsync());
    }

    [Fact]
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
        (await ScalarAsync<long>(connection, """
            SELECT COUNT(*)
            FROM
                users
            WHERE
                id = 'committed'
            """)).ShouldBe(1L);
        (await ScalarAsync<long>(connection, """
            SELECT COUNT(*)
            FROM
                users
            WHERE
                id = 'rolled-back'
            """)).ShouldBe(0L);
    }

    [Fact]
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
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await Should.ThrowAsync<InvalidOperationException>(() => database.InitializeAsync());
            await using var readonlyConnection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            await readonlyConnection.OpenAsync(TestContext.Current.CancellationToken);
            (await ScalarAsync<string>(readonlyConnection, """
                SELECT name
                FROM
                    users
                WHERE
                    id = 'id'
                """)).ShouldBe("name");
            (await ScalarAsync<long>(readonlyConnection, "PRAGMA user_version")).ShouldBe(99L);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public async Task BackupCopiesCommittedWalStateAndPassesQuickCheckAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        var request = new NotesSample.Features.Notes.SaveNoteRequest(null, null, "backup", "copy");
        await fixture.Save.Presentation.ExecuteAsync(
            new NotesSample.Application.Authentication.Principal("alice", "Alice"), request);
        var backup = Path.Combine(Path.GetDirectoryName(fixture.Path)!, "backup.sqlite");
        fixture.Database.Backup(backup);
        var check = await new Database(backup).CheckAsync();
        check.IsHealthy.ShouldBeTrue();
        await using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        (await ScalarAsync<string>(connection, """
            SELECT body
            FROM
                notes
            """)).ShouldBe("copy");
    }

    [Fact]
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
        config.Port.ShouldBe(0);
        config.PublicOrigin.ShouldBeNull();

        values["HOST"] = "0.0.0.0";
        Should.Throw<InvalidOperationException>(() => AppConfig.FromValues(name => values.GetValueOrDefault(name)));
        values["HOST"] = "127.0.0.1";
        values["AUTH_MODE"] = "proxy";
        Should.Throw<InvalidOperationException>(() => AppConfig.FromValues(name => values.GetValueOrDefault(name)));
        values["PUBLIC_ORIGIN"] = "https://example.com";
        AppConfig.FromValues(name => values.GetValueOrDefault(name)).AuthMode.ShouldBe("proxy");
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }
}
