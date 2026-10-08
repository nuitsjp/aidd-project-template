using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WpfNotesSample.Model.Domain.Notes;

namespace WpfNotesSample.Model.Infrastructure.Sqlite.Notes;

public sealed class NotesService : INotesService
{
    private readonly Database database;

    public NotesService(Database database)
    {
        this.database = database;
    }

    public Task<IReadOnlyList<Note>> ListAsync() => Task.Run<IReadOnlyList<Note>>(() =>
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, body, version, updated_at FROM notes ORDER BY updated_at DESC, id;";
        using var reader = command.ExecuteReader();
        var notes = new List<Note>();
        while (reader.Read())
        {
            notes.Add(new Note(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        return notes.AsReadOnly();
    });

    public Task<Note> SaveAsync(Guid? id, int? expectedVersion, string title, string body) => Task.Run(() =>
    {
        var input = NoteRules.Normalize(new NoteInput(title, body));
        using var connection = database.Open();
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
            command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
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
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM notes WHERE id = $id AND version = $expectedVersion;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$expectedVersion", expectedVersion);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new NoteConflictException();
        }

        transaction.Commit();
    });

    public Task<IReadOnlyList<Note>> ImportAsync(IReadOnlyList<NoteInput> inputs) => Task.Run<IReadOnlyList<Note>>(() =>
    {
        var prepared = NoteRules.NormalizeAll(inputs);
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var notes = new List<Note>();
        var updatedAt = DateTime.UtcNow;
        foreach (var input in prepared)
        {
            notes.Add(Insert(connection, transaction, input, updatedAt));
        }

        transaction.Commit();
        return notes.AsReadOnly();
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
        command.Parameters.AddWithValue("$id", note.Id.ToString("D"));
        AddInput(command, input, updatedAt);
        command.ExecuteNonQuery();
        return note;
    }

    private static void AddInput(SqliteCommand command, NoteInput input, DateTime updatedAt)
    {
        command.Parameters.AddWithValue("$title", input.Title);
        command.Parameters.AddWithValue("$body", input.Body);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O", CultureInfo.InvariantCulture));
    }
}
