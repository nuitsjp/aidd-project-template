using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Domain;
using Dapper;
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
        PersistRemove = command => PersistRemoveCore(databasePath, command);
        PersistImport = command => PersistImportCore(databasePath, command);
        NewId = NewIdCore;
        UtcNow = UtcNowCore;
        PublishChange = notifications.Publish;
    }

    internal Func<string, IReadOnlyList<Note>> ReadAll { get; init; }
    internal Func<string, string, Note> ReadOne { get; init; }
    internal Func<PreparedRemove, bool> PersistRemove { get; init; }
    internal Func<PreparedImport, int> PersistImport { get; init; }
    internal Func<Guid> NewId { get; init; }
    internal Func<DateTimeOffset> UtcNow { get; init; }
    internal Func<string, bool> PublishChange { get; init; }

    internal IReadOnlyList<Note> List(string ownerId) => ReadAll(ownerId);
    internal Note Get(string ownerId, string id) => ReadOne(ownerId, id);

    internal void Remove(string ownerId, string id, long version)
    {
        PersistRemove(new PreparedRemove(ownerId, id, version));
        Notify(ownerId);
    }

    internal static BulkPreview Preview(BulkInput input)
    {
        NoteRules.ValidateBody(input.Body);
        var rawTitles = input.Titles.Split('\n')
            .Select(line => line.EndsWith('\r') ? line[..^1] : line)
            .Where(line => line.Trim().Length > 0).ToArray();
        if (rawTitles.Length is < 1 or > 100)
        {
            throw AppFaultException.Validation("タイトルは1〜100件で入力してください。",
                new Dictionary<string, string> { ["titles"] = "1〜100行で入力してください。" });
        }

        var titles = rawTitles.Select(NoteRules.ValidateTitle).ToArray();
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

    internal static IReadOnlyList<Note> ReadAllCore(string databasePath, string ownerId)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        return connection.Query<Note>(
            "SELECT id AS Id,title AS Title,body AS Body,version AS Version,updated_at AS UpdatedAt FROM notes WHERE owner_id=@ownerId ORDER BY updated_at DESC,id",
            new { ownerId }).AsList();
    }

    internal static Note ReadOneCore(string databasePath, string ownerId, string id)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        return ReadOneFromDatabase(connection, ownerId, id);
    }

    internal static bool PersistRemoveCore(string databasePath, PreparedRemove input)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        AppDatabase.BeginImmediate(connection);
        try
        {
            var current = ReadOneFromDatabase(connection, input.OwnerId, input.Id);
            if (current.Version != input.Version)
            {
                throw new AppFaultException("EDIT_CONFLICT", "対象が更新されています。最新版を確認してください。");
            }

            connection.Execute("DELETE FROM notes WHERE owner_id=@OwnerId AND id=@Id", new { input.OwnerId, input.Id });
            AppDatabase.Commit(connection);
            return true;
        }
        catch
        {
            AppDatabase.Rollback(connection);
            throw;
        }
    }

    internal static int PersistImportCore(string databasePath, PreparedImport input)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        AppDatabase.BeginImmediate(connection);
        try
        {
            foreach (var note in input.Notes)
            {
                Insert(connection, input.OwnerId, note.Id, note.Title, note.Body, note.UpdatedAt);
            }

            AppDatabase.Commit(connection);
            return input.Notes.Count;
        }
        catch (Exception error)
        {
            AppDatabase.Rollback(connection);
            throw TranslateDatabaseError(error);
        }
    }

    internal static Guid NewIdCore() => Guid.NewGuid();
    internal static DateTimeOffset UtcNowCore() => DateTimeOffset.UtcNow;

    private static Note ReadOneFromDatabase(SqliteConnection connection, string ownerId, string id) =>
        connection.QuerySingleOrDefault<Note>(
            "SELECT id AS Id,title AS Title,body AS Body,version AS Version,updated_at AS UpdatedAt FROM notes WHERE owner_id=@ownerId AND id=@id",
            new { ownerId, id })
        ?? throw new AppFaultException("NOT_FOUND", "対象のメモが見つかりません。");

    private static void Insert(SqliteConnection connection, string ownerId, string id, string title, string body,
        DateTimeOffset updatedAt) =>
        connection.Execute(
            "INSERT INTO notes(id,owner_id,title,body,version,updated_at) VALUES(@id,@ownerId,@title,@body,1,@updatedAt)",
            new { ownerId, id, title, body, updatedAt = updatedAt.ToUniversalTime().ToString("O") });

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

internal sealed record BulkInput(string Titles, string Body);
internal sealed record BulkPreview(IReadOnlyList<string> Titles, string Body);
internal sealed record BulkResult(int Count);
internal sealed record PreparedRemove(string OwnerId, string Id, long Version);
internal sealed record PreparedInsert(string Id, string Title, string Body, DateTimeOffset UpdatedAt);
internal sealed record PreparedImport(string OwnerId, IReadOnlyList<PreparedInsert> Notes);
