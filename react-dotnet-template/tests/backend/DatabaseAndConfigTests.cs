using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class DatabaseAndConfigTests
{
    [TestMethod]
    public void DatabaseEnablesMigrationWalAndForeignKeys()
    {
        using var fixture = new TestDatabase();
        using var connection = AppDatabase.OpenConnection(fixture.Path);
        Assert.AreEqual(1L, Scalar<long>(connection, "PRAGMA user_version"));
        Assert.AreEqual("wal", Scalar<string>(connection, "PRAGMA journal_mode"));
        Assert.AreEqual(1L, Scalar<long>(connection, "PRAGMA foreign_keys"));
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO notes VALUES('n','missing','a','',1,'time')";
        TestAssert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    public void ReopenPreservesDataAndUnknownSchemaIsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-version-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "app.sqlite");
        try
        {
            AppDatabase.Initialize(path);
            using (var connection = AppDatabase.OpenConnection(path))
            {
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO users VALUES('id','name'); PRAGMA user_version=99;";
                command.ExecuteNonQuery();
            }

            TestAssert.Throws<InvalidOperationException>(() => AppDatabase.Initialize(path));
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
    public void BackupCopiesCommittedWalStateAndPassesQuickCheck()
    {
        using var fixture = new TestDatabase();
        fixture.Save.Execute("alice", new Aidd.ReactDotnet.Features.Notes.SaveNote.Input(null, null, "backup", "copy"));
        var backup = Path.Combine(Path.GetDirectoryName(fixture.Path)!, "backup.sqlite");
        AppDatabase.Backup(fixture.Path, backup);
        var check = AppDatabase.Check(backup);
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
