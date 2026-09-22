using System.Text;
using Aidd.ReactDotnet.Shared;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed class NotesService
{
    private readonly Action<Exception> reportNotificationError;

    internal NotesService(string databasePath, ChangeNotifications notifications, Action<Exception> reportNotificationError)
    {
        this.reportNotificationError = reportNotificationError;
        ReadAll = ownerId => ReadAllCore(databasePath, ownerId);
        ReadOne = (ownerId, id) => ReadOneCore(databasePath, ownerId, id);
        PersistSave = command => PersistSaveCore(databasePath, command);
        PersistRemove = command => PersistRemoveCore(databasePath, command);
        PersistImport = command => PersistImportCore(databasePath, command);
        NewId = NewIdCore;
        UtcNow = UtcNowCore;
        PublishChange = notifications.Publish;
    }

    internal Func<string, IReadOnlyList<Note>> ReadAll { get; init; }
    internal Func<string, string, Note> ReadOne { get; init; }
    internal Func<PreparedSave, Note> PersistSave { get; init; }
    internal Func<PreparedRemove, bool> PersistRemove { get; init; }
    internal Func<PreparedImport, int> PersistImport { get; init; }
    internal Func<Guid> NewId { get; init; }
    internal Func<DateTimeOffset> UtcNow { get; init; }
    internal Func<string, bool> PublishChange { get; init; }

    internal IReadOnlyList<Note> List(string ownerId) => ReadAll(ownerId);
    internal Note Get(string ownerId, string id) => ReadOne(ownerId, id);

    internal Note Save(string ownerId, SaveNote input)
    {
        var title = ValidateTitle(input.Title);
        ValidateBody(input.Body);
        if ((input.Id is null) != (input.Version is null))
        {
            throw AppFaultException.Validation("編集対象と版を指定してください。");
        }

        var prepared = new PreparedSave(ownerId, input.Id ?? NewId().ToString("D"), input.Version,
            title, input.Body, UtcNow());
        var saved = PersistSave(prepared);
        Notify(ownerId);
        return saved;
    }

    internal void Remove(string ownerId, string id, long version)
    {
        PersistRemove(new PreparedRemove(ownerId, id, version));
        Notify(ownerId);
    }

    internal static BulkPreview Preview(BulkInput input)
    {
        ValidateBody(input.Body);
        var rawTitles = input.Titles.Split('\n')
            .Select(line => line.EndsWith('\r') ? line[..^1] : line)
            .Where(line => line.Trim().Length > 0).ToArray();
        if (rawTitles.Length is < 1 or > 100)
        {
            throw AppFaultException.Validation("タイトルは1〜100件で入力してください。",
                new Dictionary<string, string> { ["titles"] = "1〜100行で入力してください。" });
        }

        var titles = rawTitles.Select(ValidateTitle).ToArray();
        if (titles.Distinct(StringComparer.Ordinal).Count() != titles.Length)
        {
            throw AppFaultException.Validation("入力内でタイトルが重複しています。",
                new Dictionary<string, string> { ["titles"] = "重複を取り除いてください。" });
        }

        return new BulkPreview(titles, input.Body);
    }

    internal BulkResult ImportMany(string ownerId, BulkInput input)
    {
        var preview = Preview(input);
        var notes = preview.Titles
            .Select(title => new PreparedInsert(NewId().ToString("D"), title, preview.Body, UtcNow()))
            .ToArray();
        var count = PersistImport(new PreparedImport(ownerId, notes));
        Notify(ownerId);
        return new BulkResult(count);
    }

    internal static string ValidateTitle(string value)
    {
        var title = value.Trim();
        if (title.Length == 0 || title.EnumerateRunes().Count() > 100)
        {
            throw AppFaultException.Validation("タイトルは1〜100文字で入力してください。",
                new Dictionary<string, string> { ["title"] = "1〜100文字で入力してください。" });
        }

        return title;
    }

    internal static void ValidateBody(string value)
    {
        if (value.EnumerateRunes().Count() > 10_000)
        {
            throw AppFaultException.Validation("本文は10,000文字以内で入力してください。",
                new Dictionary<string, string> { ["body"] = "10,000文字以内で入力してください。" });
        }
    }

    internal static IReadOnlyList<Note> ReadAllCore(string databasePath, string ownerId)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,title,body,version,updated_at FROM notes WHERE owner_id=$ownerId ORDER BY updated_at DESC,id";
        command.Parameters.AddWithValue("$ownerId", ownerId);
        using var reader = command.ExecuteReader();
        var result = new List<Note>();
        while (reader.Read())
        {
            result.Add(ReadNote(reader));
        }

        return result;
    }

    internal static Note ReadOneCore(string databasePath, string ownerId, string id)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        return ReadOneFromDatabase(connection, ownerId, id);
    }

    internal static Note PersistSaveCore(string databasePath, PreparedSave input)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        BeginImmediate(connection);
        try
        {
            if (input.Version is not null)
            {
                var current = ReadOneFromDatabase(connection, input.OwnerId, input.Id);
                if (current.Version != input.Version)
                {
                    throw new AppFaultException("EDIT_CONFLICT",
                        "別の操作で更新されています。下書きを保持したまま、最新版を確認してください。");
                }

                using var update = connection.CreateCommand();
                update.CommandText = "UPDATE notes SET title=$title,body=$body,version=version+1,updated_at=$updatedAt WHERE owner_id=$ownerId AND id=$id";
                AddNoteParameters(update, input.OwnerId, input.Id, input.Title, input.Body, input.UpdatedAt);
                update.ExecuteNonQuery();
            }
            else
            {
                Insert(connection, input.OwnerId, input.Id, input.Title, input.Body, input.UpdatedAt);
            }

            var saved = ReadOneFromDatabase(connection, input.OwnerId, input.Id);
            Commit(connection);
            return saved;
        }
        catch (Exception error)
        {
            Rollback(connection);
            throw TranslateDatabaseError(error);
        }
    }

    internal static bool PersistRemoveCore(string databasePath, PreparedRemove input)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        BeginImmediate(connection);
        try
        {
            var current = ReadOneFromDatabase(connection, input.OwnerId, input.Id);
            if (current.Version != input.Version)
            {
                throw new AppFaultException("EDIT_CONFLICT", "対象が更新されています。最新版を確認してください。");
            }

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM notes WHERE owner_id=$ownerId AND id=$id";
            command.Parameters.AddWithValue("$ownerId", input.OwnerId);
            command.Parameters.AddWithValue("$id", input.Id);
            command.ExecuteNonQuery();
            Commit(connection);
            return true;
        }
        catch
        {
            Rollback(connection);
            throw;
        }
    }

    internal static int PersistImportCore(string databasePath, PreparedImport input)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        BeginImmediate(connection);
        try
        {
            foreach (var note in input.Notes)
            {
                Insert(connection, input.OwnerId, note.Id, note.Title, note.Body, note.UpdatedAt);
            }

            Commit(connection);
            return input.Notes.Count;
        }
        catch (Exception error)
        {
            Rollback(connection);
            throw TranslateDatabaseError(error);
        }
    }

    internal static Guid NewIdCore() => Guid.NewGuid();
    internal static DateTimeOffset UtcNowCore() => DateTimeOffset.UtcNow;

    private static Note ReadOneFromDatabase(SqliteConnection connection, string ownerId, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,title,body,version,updated_at FROM notes WHERE owner_id=$ownerId AND id=$id";
        command.Parameters.AddWithValue("$ownerId", ownerId);
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new AppFaultException("NOT_FOUND", "対象のメモが見つかりません。");
        }

        return ReadNote(reader);
    }

    private static Note ReadNote(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1),
        reader.GetString(2), reader.GetInt64(3), reader.GetString(4));

    private static void Insert(SqliteConnection connection, string ownerId, string id, string title, string body,
        DateTimeOffset updatedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO notes(id,owner_id,title,body,version,updated_at) VALUES($id,$ownerId,$title,$body,1,$updatedAt)";
        AddNoteParameters(command, ownerId, id, title, body, updatedAt);
        command.ExecuteNonQuery();
    }

    private static void AddNoteParameters(SqliteCommand command, string ownerId, string id, string title, string body,
        DateTimeOffset updatedAt)
    {
        command.Parameters.AddWithValue("$ownerId", ownerId);
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$body", body);
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToUniversalTime().ToString("O"));
    }

    private static void BeginImmediate(SqliteConnection connection) => Execute(connection, "BEGIN IMMEDIATE;");
    private static void Commit(SqliteConnection connection) => Execute(connection, "COMMIT;");
    private static void Rollback(SqliteConnection connection) => Execute(connection, "ROLLBACK;");

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static Exception TranslateDatabaseError(Exception error)
    {
        return error is SqliteException { SqliteExtendedErrorCode: 2067 }
            ? new AppFaultException("TITLE_EXISTS", "同じタイトルのメモが既にあります。",
                new Dictionary<string, string> { ["title"] = "別のタイトルを指定してください。" })
            : error;
    }

    private void Notify(string ownerId)
    {
        try
        {
            PublishChange(ownerId);
        }
        catch (Exception error)
        {
            reportNotificationError(error);
        }
    }
}

internal sealed record Note(string Id, string Title, string Body, long Version, string UpdatedAt);
internal sealed record SaveNote(string? Id, long? Version, string Title, string Body);
internal sealed record BulkInput(string Titles, string Body);
internal sealed record BulkPreview(IReadOnlyList<string> Titles, string Body);
internal sealed record BulkResult(int Count);
internal sealed record PreparedSave(string OwnerId, string Id, long? Version, string Title, string Body, DateTimeOffset UpdatedAt);
internal sealed record PreparedRemove(string OwnerId, string Id, long Version);
internal sealed record PreparedInsert(string Id, string Title, string Body, DateTimeOffset UpdatedAt);
internal sealed record PreparedImport(string OwnerId, IReadOnlyList<PreparedInsert> Notes);
