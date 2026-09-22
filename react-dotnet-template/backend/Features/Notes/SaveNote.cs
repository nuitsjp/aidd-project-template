using Aidd.ReactDotnet.Presentation.Http;
using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Domain;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed class SaveNote
{
    private readonly Action<Exception> reportNotificationError;

    internal SaveNote(string databasePath, ChangeNotifications notifications, Action<Exception> reportNotificationError)
    {
        this.reportNotificationError = reportNotificationError;
        PersistSave = input => PersistSaveCore(databasePath, input);
        NewId = NewIdCore;
        UtcNow = UtcNowCore;
        PublishChange = notifications.Publish;
    }

    internal Func<PreparedSave, Note> PersistSave { get; init; }
    internal Func<Guid> NewId { get; init; }
    internal Func<DateTimeOffset> UtcNow { get; init; }
    internal Func<string, bool> PublishChange { get; init; }

    internal void Map(WebApplication app, IdentityService identity)
    {
        app.MapPost("/api/notes/save", async (HttpContext context) =>
        {
            var user = identity.Resolve(context.Request)
                ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");
            var input = await ReadInput(context.Request, context.RequestAborted);
            return Results.Ok(Execute(user.Id, input));
        });
    }

    internal static async Task<Input> ReadInput(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await JsonRequest.ReadObject(request, new[] { "id", "version", "title", "body" }, cancellationToken);
        var root = document.RootElement;
        var title = JsonRequest.RequiredString(root, "title");
        var body = JsonRequest.RequiredString(root, "body");
        string? id = null;
        long? version = null;
        if (root.TryGetProperty("id", out var idElement))
        {
            id = JsonRequest.String(idElement);
            if (!JsonRequest.IsUuid(id))
            {
                throw AppFaultException.Validation();
            }
        }

        if (root.TryGetProperty("version", out var versionElement))
        {
            version = JsonRequest.PositiveInteger(versionElement);
        }

        return new Input(id, version, title, body);
    }

    internal Note Execute(string ownerId, Input input)
    {
        var validated = ValidateInput(input);
        var prepared = new PreparedSave(ownerId, validated.Id ?? NewId().ToString("D"), validated.Version,
            validated.Title, validated.Body, UtcNow());
        var saved = PersistSave(prepared);
        Notify(ownerId);
        return saved;
    }

    internal static Input ValidateInput(Input input)
    {
        var title = NoteRules.ValidateTitle(input.Title);
        NoteRules.ValidateBody(input.Body);
        if ((input.Id is null) != (input.Version is null))
        {
            throw AppFaultException.Validation("編集対象と版を指定してください。");
        }

        return input with { Title = title };
    }

    internal static Note PersistSaveCore(string databasePath, PreparedSave input)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        AppDatabase.BeginImmediate(connection);
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

                connection.Execute(
                    "UPDATE notes SET title=@Title,body=@Body,version=version+1,updated_at=@UpdatedAt WHERE owner_id=@OwnerId AND id=@Id",
                    new { input.OwnerId, input.Id, input.Title, input.Body, UpdatedAt = input.UpdatedAt.ToUniversalTime().ToString("O") });
            }
            else
            {
                Insert(connection, input.OwnerId, input.Id, input.Title, input.Body, input.UpdatedAt);
            }

            var saved = ReadOneFromDatabase(connection, input.OwnerId, input.Id);
            AppDatabase.Commit(connection);
            return saved;
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

    internal sealed record Input(string? Id, long? Version, string Title, string Body);
    internal sealed record PreparedSave(string OwnerId, string Id, long? Version, string Title, string Body, DateTimeOffset UpdatedAt);
}
