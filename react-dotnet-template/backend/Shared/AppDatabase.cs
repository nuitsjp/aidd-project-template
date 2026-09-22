using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Shared;

internal static class AppDatabase
{
    internal static void Initialize(string path)
    {
        if (path == ":memory:")
        {
            throw new InvalidOperationException("このアプリはファイルDBを使用します。テストも専用ファイルを指定してください。");
        }

        var absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        using var connection = OpenConnection(absolute);
        Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;");
        Execute(connection, "BEGIN IMMEDIATE;");
        try
        {
            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version";
            var version = Convert.ToInt32(versionCommand.ExecuteScalar());
            if (version is < 0 or > 1)
            {
                throw new InvalidOperationException("未対応のDBスキーマです。");
            }

            if (version == 0)
            {
                using var stream = typeof(AppDatabase).Assembly.GetManifestResourceStream("App.Migrations.001-notes.sql")
                    ?? throw new InvalidOperationException("DB migrationが見つかりません。");
                using var reader = new StreamReader(stream);
                Execute(connection, reader.ReadToEnd());
                Execute(connection, "PRAGMA user_version=1;");
            }

            Execute(connection, "COMMIT;");
        }
        catch
        {
            Execute(connection, "ROLLBACK;");
            throw;
        }
    }

    internal static SqliteConnection OpenConnection(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=2000; PRAGMA synchronous=FULL;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal static void Backup(string sourcePath, string destinationPath)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (source.Equals(destination, comparison) || File.Exists(destination))
        {
            throw new InvalidOperationException("バックアップ先は未作成の別ファイルにしてください。");
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException("バックアップ元DBが見つかりません。", source);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var sourceConnection = OpenReadOnly(source);
        using var destinationConnection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destination,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection);
    }

    internal static DatabaseCheck Check(string path)
    {
        using var connection = OpenReadOnly(Path.GetFullPath(path));
        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(versionCommand.ExecuteScalar());
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA quick_check";
        using var reader = checkCommand.ExecuteReader();
        var results = new List<string>();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return new DatabaseCheck(version, results);
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA busy_timeout=2000;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

internal sealed record DatabaseCheck(int Version, IReadOnlyList<string> Result)
{
    internal bool IsHealthy => Result.Count > 0 && Result.All(value => value == "ok");
}
