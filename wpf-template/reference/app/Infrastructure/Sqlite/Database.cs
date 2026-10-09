using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WpfNotesSample.Infrastructure.Sqlite;

public sealed class Database
{
    private readonly string _filePath;

    public Database(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var versionCommand = connection.CreateCommand();
        versionCommand.Transaction = transaction;
        versionCommand.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(versionCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version > 1)
        {
            throw new InvalidOperationException("未対応のDBスキーマです。");
        }

        if (version == 0)
        {
            using var stream = typeof(Database).Assembly.GetManifestResourceStream("App.Migrations.001-notes.sql")
                ?? throw new InvalidOperationException("DB移行ファイルが見つかりません。");
            using var reader = new StreamReader(stream);
            using var migrationCommand = connection.CreateCommand();
            migrationCommand.Transaction = transaction;
            migrationCommand.CommandText = reader.ReadToEnd();
            migrationCommand.ExecuteNonQuery();
            migrationCommand.CommandText = "PRAGMA user_version = 1;";
            migrationCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    internal SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _filePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 2,
        }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA synchronous = FULL;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
