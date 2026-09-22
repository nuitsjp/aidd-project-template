using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Domain;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed class NotesService
{
    internal NotesService(Database database, ChangeNotifications notifications)
    {
        ReadAll = ownerId => database.WithConnection(connection => ReadAllCore(connection, ownerId));
        ReadOne = (ownerId, id) => database.WithConnection(connection => ReadOneCore(connection, ownerId, id));
        PersistRemove = command => database.WithImmediateTransaction(
            connection => PersistRemoveCore(connection, command));
        PersistImport = command =>
        {
            try
            {
                return database.WithImmediateTransaction(
                    connection => PersistImportCore(connection, command));
            }
            catch (Exception error)
            {
                throw TranslateDatabaseError(error);
            }
        };
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
        PublishChange(ownerId);
    }

    internal static BulkPreview Preview(BulkInput input)
    {
        NoteRules.ValidateBody(input.Body);
        var rawTitles = input.Titles.Split('\n')
            .Select(line => line.EndsWith('\r') ? line[..^1] : line)
            .Where(line => line.Trim().Length > 0).ToArray();
        if (rawTitles.Length is < 1 or > 100)
        {
            throw AppFaultException.Validation("タイトルは1〜100件で入力してください。");
        }

        var titles = rawTitles.Select(NoteRules.NormalizeTitle).ToArray();
        foreach (var title in titles)
        {
            NoteRules.ValidateTitle(title);
        }
        if (titles.Distinct(StringComparer.Ordinal).Count() != titles.Length)
        {
            throw AppFaultException.Validation("入力内でタイトルが重複しています。");
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
        PublishChange(ownerId);
        return new BulkResult(count);
    }

    internal static IReadOnlyList<Note> ReadAllCore(SqliteConnection connection, string ownerId) =>
        connection.Query<Note>(
            "SELECT id AS Id,title AS Title,body AS Body,version AS Version,updated_at AS UpdatedAt FROM notes WHERE owner_id=@ownerId ORDER BY updated_at DESC,id",
            new { ownerId }).AsList();

    internal static Note ReadOneCore(SqliteConnection connection, string ownerId, string id) =>
        ReadOneFromDatabase(connection, ownerId, id);

    internal static bool PersistRemoveCore(SqliteConnection connection, PreparedRemove input)
    {
        var current = ReadOneFromDatabase(connection, input.OwnerId, input.Id);
        if (current.Version != input.Version)
        {
            throw new AppFaultException("EDIT_CONFLICT", "対象が更新されています。最新版を確認してください。");
        }

        connection.Execute("DELETE FROM notes WHERE owner_id=@OwnerId AND id=@Id", new { input.OwnerId, input.Id });
        return true;
    }

    internal static int PersistImportCore(SqliteConnection connection, PreparedImport input)
    {
        foreach (var note in input.Notes)
        {
            Insert(connection, input.OwnerId, note.Id, note.Title, note.Body, note.UpdatedAt);
        }

        return input.Notes.Count;
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
            ? new AppFaultException("TITLE_EXISTS", "同じタイトルのメモが既にあります。")
            : error;
    }

}

internal sealed record BulkInput([property: System.Text.Json.Serialization.JsonRequired] string Titles, [property: System.Text.Json.Serialization.JsonRequired] string Body);
internal sealed record BulkPreview(IReadOnlyList<string> Titles, string Body);
internal sealed record BulkResult(int Count);
internal sealed record PreparedRemove(string OwnerId, string Id, long Version);
internal sealed record PreparedInsert(string Id, string Title, string Body, DateTimeOffset UpdatedAt);
internal sealed record PreparedImport(string OwnerId, IReadOnlyList<PreparedInsert> Notes);
