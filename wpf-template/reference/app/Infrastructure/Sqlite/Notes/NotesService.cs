using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WpfNotesSample.Domain.Notes;

namespace WpfNotesSample.Infrastructure.Sqlite.Notes;

public sealed class NotesService : INotesService
{
    private const int IdColumn = 0;
    private const int TitleColumn = 1;
    private const int BodyColumn = 2;
    private const int VersionColumn = 3;
    private const int UpdatedAtColumn = 4;

    private readonly Database _database;

    public NotesService(Database database)
    {
        _database = database;
    }

    public Task<IReadOnlyList<Note>> ListAsync() => Task.Run<IReadOnlyList<Note>>(() =>
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, body, version, updated_at FROM notes ORDER BY updated_at DESC, id;";
        using var reader = command.ExecuteReader();
        var notes = new List<Note>();
        while (reader.Read())
        {
            notes.Add(ReadNote(reader));
        }

        return notes.AsReadOnly();
    });

    public Task<Note?> GetAsync(Guid id) => Task.Run<Note?>(() =>
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, body, version, updated_at FROM notes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", ToKey(id));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return ReadNote(reader);
    });

    public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body) => Task.Run(() =>
    {
        var input = NoteRules.Normalize(new NoteInput(title, body));
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var updatedAt = DateTime.UtcNow;
        Note note;
        if (id is null)
        {
            note = Insert(connection, transaction, input, updatedAt);
        }
        else
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE notes
                SET title = $title, body = $body, version = version + 1, updated_at = $updatedAt
                WHERE id = $id AND version = $expectedVersion;
                """;
            command.Parameters.AddWithValue("$id", ToKey(id.Value));
            command.Parameters.AddWithValue("$expectedVersion", expectedVersion.GetValueOrDefault());
            AddInput(command, input, updatedAt);
            if (command.ExecuteNonQuery() != 1)
            {
                throw new NoteConflictException();
            }

            note = new Note(id.Value, input.Title, input.Body, expectedVersion!.Value + 1, updatedAt);
        }

        transaction.Commit();
        return note;
    });

    public Task RemoveAsync(Guid id, int expectedVersion) => Task.Run(() =>
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM notes WHERE id = $id AND version = $expectedVersion;";
        command.Parameters.AddWithValue("$id", ToKey(id));
        command.Parameters.AddWithValue("$expectedVersion", expectedVersion);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new NoteConflictException();
        }

        transaction.Commit();
    });

    private static Note Insert(SqliteConnection connection, SqliteTransaction transaction, NoteInput input, DateTime updatedAt)
    {
        var note = new Note(Guid.NewGuid(), input.Title, input.Body, 1, updatedAt);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO notes (id, title, body, version, updated_at)
            VALUES ($id, $title, $body, 1, $updatedAt);
            """;
        command.Parameters.AddWithValue("$id", ToKey(note.Id));
        AddInput(command, input, updatedAt);
        command.ExecuteNonQuery();
        return note;
    }

    // SELECT id, title, body, version, updated_at の 1 行をノートに変換する。
    private static Note ReadNote(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(IdColumn)),
        reader.GetString(TitleColumn),
        reader.GetString(BodyColumn),
        reader.GetInt32(VersionColumn),
        DateTime.Parse(reader.GetString(UpdatedAtColumn), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static string ToKey(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static void AddInput(SqliteCommand command, NoteInput input, DateTime updatedAt)
    {
        command.Parameters.AddWithValue("$title", input.Title);
        command.Parameters.AddWithValue("$body", input.Body);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
    }
}
