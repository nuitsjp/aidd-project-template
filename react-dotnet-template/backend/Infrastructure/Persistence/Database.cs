using Dapper;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Infrastructure.Persistence;

internal sealed class Database
{
    private readonly string path;

    internal Database(string databasePath)
    {
        if (databasePath == ":memory:")
        {
            throw new InvalidOperationException("このアプリはファイルDBを使用します。テストも専用ファイルを指定してください。");
        }

        path = Path.GetFullPath(databasePath);
    }

    internal void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = Open();
        connection.Execute("""
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            """);
        connection.Execute("""
            BEGIN IMMEDIATE;
            """);
        try
        {
            var version = connection.ExecuteScalar<int>("""
                PRAGMA user_version;
                """);
            if (version is < 0 or > 1)
            {
                throw new InvalidOperationException("未対応のDBスキーマです。");
            }

            if (version == 0)
            {
                using var stream = typeof(Database).Assembly.GetManifestResourceStream("App.Migrations.001-notes.sql")
                    ?? throw new InvalidOperationException("DB migrationが見つかりません。");
                using var reader = new StreamReader(stream);
                connection.Execute(reader.ReadToEnd());
                connection.Execute("""
                    PRAGMA user_version = 1;
                    """);
            }

            connection.Execute("""
                COMMIT;
                """);
        }
        catch
        {
            connection.Execute("""
                ROLLBACK;
                """);
            throw;
        }
    }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        try
        {
            connection.Open();
            connection.Execute("""
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 2000;
                PRAGMA synchronous = FULL;
                """);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal T WithConnection<T>(Func<SqliteConnection, T> execute)
    {
        using var connection = Open();
        return execute(connection);
    }

    internal T WithImmediateTransaction<T>(Func<SqliteConnection, T> execute)
    {
        using var connection = Open();
        connection.Execute("""
            BEGIN IMMEDIATE;
            """);
        try
        {
            var result = execute(connection);
            connection.Execute("""
                COMMIT;
                """);
            return result;
        }
        catch
        {
            connection.Execute("""
                ROLLBACK;
                """);
            throw;
        }
    }

    internal async Task<ITransaction> BeginTransactionAsync(TransactionMode mode = TransactionMode.Immediate)
    {
        var connection = await OpenAsync();
        try
        {
            var begin = mode switch
            {
                TransactionMode.Deferred => """
                    BEGIN DEFERRED;
                    """,
                TransactionMode.Immediate => """
                    BEGIN IMMEDIATE;
                    """,
                TransactionMode.Exclusive => """
                    BEGIN EXCLUSIVE;
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            await connection.ExecuteAsync(begin);
            return new DatabaseTransaction(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal void Backup(string destinationPath)
    {
        var source = path;
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

    internal DatabaseCheck Check()
    {
        using var connection = OpenReadOnly(path);
        var version = connection.ExecuteScalar<int>("""
            PRAGMA user_version;
            """);
        var results = connection.Query<string>("""
            PRAGMA quick_check;
            """).AsList();

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
            connection.Execute("""
                PRAGMA busy_timeout = 2000;
                """);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        try
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync("""
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 2000;
                PRAGMA synchronous = FULL;
                """);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

internal enum TransactionMode
{
    Deferred,
    Immediate,
    Exclusive,
}

internal interface ITransaction : IAsyncDisposable
{
    SqliteConnection Connection { get; }

    Task CommitAsync();

    Task RollbackAsync();
}

internal sealed class DatabaseTransaction(SqliteConnection connection) : ITransaction
{
    private bool completed;

    public SqliteConnection Connection { get; } = connection;

    public async Task CommitAsync()
    {
        if (completed) return;
        await Connection.ExecuteAsync("""
            COMMIT;
            """);
        completed = true;
    }

    public async Task RollbackAsync()
    {
        if (completed) return;
        await Connection.ExecuteAsync("""
            ROLLBACK;
            """);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await RollbackAsync();
        }
        finally
        {
            await Connection.DisposeAsync();
        }
    }
}

internal sealed record DatabaseCheck(int Version, IReadOnlyList<string> Result)
{
    internal bool IsHealthy => Result.Count > 0 && Result.All(value => value == "ok");
}
