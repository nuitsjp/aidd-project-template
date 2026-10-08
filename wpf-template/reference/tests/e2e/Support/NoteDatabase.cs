using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WpfNotesSample.E2eTests.Support;

internal sealed class NoteDatabase
{
    private readonly string dataDirectory;

    public NoteDatabase(string dataDirectory) => this.dataDirectory = dataDirectory;

    public List<SavedNote> ReadNotes()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT title, body, version FROM notes ORDER BY title;";
        using var reader = command.ExecuteReader();
        var notes = new List<SavedNote>();
        while (reader.Read())
            notes.Add(new SavedNote(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        return notes;
    }

    public void ExecuteSql(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public Guid ReadNoteId(string title)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM notes WHERE title = $title;";
        command.Parameters.AddWithValue("$title", title);
        return Guid.Parse((string)command.ExecuteScalar()!);
    }

    public SqliteConnection HoldWriteLock()
    {
        var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "BEGIN IMMEDIATE;";
        command.ExecuteNonQuery();
        return connection;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, "notes.db"),
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }
}
