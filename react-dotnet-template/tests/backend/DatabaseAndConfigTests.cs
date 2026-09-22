using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Configuration;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class DatabaseAndConfigTests
{
    [TestMethod]
    public void DatabaseInstancesKeepPathsIndependent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-database-instances-{Guid.NewGuid():N}");
        try
        {
            var first = new Database(Path.Combine(directory, "first.sqlite"));
            var second = new Database(Path.Combine(directory, "second.sqlite"));
            first.Initialize();
            second.Initialize();

            first.WithConnection(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO users VALUES('first','First');";
                return command.ExecuteNonQuery();
            });

            using var firstConnection = first.Open();
            using var secondConnection = second.Open();
            Assert.AreEqual(1L, Scalar<long>(firstConnection, "SELECT COUNT(*) FROM users"));
            Assert.AreEqual(0L, Scalar<long>(secondConnection, "SELECT COUNT(*) FROM users"));
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
    public void DatabaseEnablesMigrationWalAndForeignKeys()
    {
        using var fixture = new TestDatabase();
        using var connection = fixture.Database.Open();
        Assert.AreEqual(1L, Scalar<long>(connection, "PRAGMA user_version"));
        Assert.AreEqual("wal", Scalar<string>(connection, "PRAGMA journal_mode"));
        Assert.AreEqual(1L, Scalar<long>(connection, "PRAGMA foreign_keys"));
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO notes VALUES('n','missing','a','',1,'time')";
        TestAssert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    public async Task TransactionExposesConnectionAndSupportsCommitRollbackAndMode()
    {
        using var fixture = new TestDatabase();

        await using (var transaction = await fixture.Database.BeginTransactionAsync())
        {
            await transaction.Connection.ExecuteAsync("INSERT INTO users VALUES('committed','Committed');");
            await transaction.CommitAsync();
        }

        await using (var transaction = await fixture.Database.BeginTransactionAsync(TransactionMode.Deferred))
        {
            await transaction.Connection.ExecuteAsync("INSERT INTO users VALUES('rolled-back','Rolled Back');");
            await transaction.RollbackAsync();
        }

        using var connection = fixture.Database.Open();
        Assert.AreEqual(1L, Scalar<long>(connection, "SELECT COUNT(*) FROM users WHERE id='committed'"));
        Assert.AreEqual(0L, Scalar<long>(connection, "SELECT COUNT(*) FROM users WHERE id='rolled-back'"));
    }

    [TestMethod]
    public void ReopenPreservesDataAndUnknownSchemaIsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-version-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "app.sqlite");
        try
        {
            var database = new Database(path);
            database.Initialize();
            using (var connection = database.Open())
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO users VALUES('id','name'); PRAGMA user_version=99;";
                command.ExecuteNonQuery();
            }

            TestAssert.Throws<InvalidOperationException>(() => database.Initialize());
            using var readonlyConnection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            readonlyConnection.Open();
            Assert.AreEqual("name", Scalar<string>(readonlyConnection, "SELECT name FROM users WHERE id='id'"));
            Assert.AreEqual(99L, Scalar<long>(readonlyConnection, "PRAGMA user_version"));
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
    public async Task BackupCopiesCommittedWalStateAndPassesQuickCheck()
    {
        using var fixture = new TestDatabase();
        var request = new Aidd.ReactDotnet.Features.Notes.SaveNoteRequest(null, null, "backup", "copy");
        Assert.IsTrue(fixture.Save.Presentation.TryValidate(request, out var errors),
            errors is null ? string.Empty : string.Join("; ", errors.SelectMany(item => item.Value)));
        await fixture.Save.Presentation.ExecuteAsync(
            new Aidd.ReactDotnet.Application.Authentication.Principal("alice", "Alice"), request);
        var backup = Path.Combine(Path.GetDirectoryName(fixture.Path)!, "backup.sqlite");
        fixture.Database.Backup(backup);
        var check = new Database(backup).Check();
        Assert.IsTrue(check.IsHealthy);
        using var connection = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
        connection.Open();
        Assert.AreEqual("copy", Scalar<string>(connection, "SELECT body FROM notes"));
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

    private static T Scalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }
}
