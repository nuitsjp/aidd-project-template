using NotesSample.Application;
using NotesSample.Application.Authentication;
using NotesSample.Domain;
using NotesSample.Domain.Notes;
using NotesSample.Infrastructure.Authentication;
using NotesSample.Infrastructure.Notifications;
using NotesSample.Infrastructure.Persistence;
using NotesSample.Presentation.Http;
using NotesSample.Presentation.Http.Validation;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NotesSample.Features.Notes;

internal sealed class RemoveNote
{
    internal RemoveNote(Database database, ChangeNotifications notifications)
    {
        Presentation = new PresentationLayer(new ApplicationLayer(database, notifications));
    }

    internal PresentationLayer Presentation { get; }

    internal void Map(WebApplication app, IdentityService identity)
    {
        app.MapPost("/api/notes/remove", async (HttpContext context, RemoveNoteInput input) =>
        {
            var principal = identity.Resolve(context.Request)
                ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");
            return await Presentation.ExecuteAsync(principal, input);
        })
            .WithName(nameof(RemoveNote))
            .Produces<SuccessOutput>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
            .ProducesCommonPostErrors();
    }

    internal sealed class PresentationLayer(IApplicationLayer<RemoveNoteInput, RemoveResult> application)
    {
        internal Func<Principal, RemoveNoteInput, Task<RemoveResult>> ExecuteApplicationAsync { get; set; } =
            application.ExecuteAsync;

        internal async Task<IResult> ExecuteAsync(Principal principal, RemoveNoteInput input) =>
            await ExecuteApplicationAsync(principal, input) switch
            {
                RemoveResult.Success => TypedResults.Ok(new SuccessOutput(true)),
                RemoveResult.NotFound => TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "見つかりません。",
                    detail: "対象のメモが見つかりません。"),
                RemoveResult.Conflict => TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "競合が発生しました。",
                    detail: "対象が更新されています。最新版を確認してください。"),
                _ => throw new InvalidOperationException("未対応の削除結果です。"),
            };
    }

    internal sealed class ApplicationLayer : IApplicationLayer<RemoveNoteInput, RemoveResult>
    {
        private readonly Database database;

        internal ApplicationLayer(Database database, ChangeNotifications notifications)
        {
            this.database = database;
            ReadAsync = PersistenceLayer.ReadAsync;
            DeleteAsync = PersistenceLayer.DeleteAsync;
            PublishChange = notifications.Publish;
        }

        internal Func<SqliteConnection, string, string, Task<Note?>> ReadAsync { get; set; }
        internal Func<SqliteConnection, string, string, Task<int>> DeleteAsync { get; set; }
        internal Action<string> PublishChange { get; set; }

        public async Task<RemoveResult> ExecuteAsync(Principal principal, RemoveNoteInput input)
        {
            await using var transaction = await database.BeginTransactionAsync();
            var current = await ReadAsync(transaction.Connection, principal.Id, input.Id);
            if (current is null)
            {
                return new RemoveResult.NotFound();
            }

            if (current.Version != input.Version)
            {
                return new RemoveResult.Conflict();
            }

            await DeleteAsync(transaction.Connection, principal.Id, input.Id);
            await transaction.CommitAsync();
            PublishChange(principal.Id);
            return new RemoveResult.Success();
        }
    }

    internal static class PersistenceLayer
    {
        internal static Task<Note?> ReadAsync(SqliteConnection connection, string ownerId, string id) =>
            connection.QuerySingleOrDefaultAsync<Note>(
                """
                SELECT
                    id AS Id,
                    title AS Title,
                    body AS Body,
                    version AS Version,
                    updated_at AS UpdatedAt
                FROM
                    notes
                WHERE
                    owner_id = @ownerId AND id = @id
                """,
                new { ownerId, id });

        internal static Task<int> DeleteAsync(SqliteConnection connection, string ownerId, string id) =>
            connection.ExecuteAsync(
                """
                DELETE FROM notes
                WHERE
                    owner_id = @ownerId AND id = @id
                """,
                new { ownerId, id });
    }

    internal abstract record RemoveResult
    {
        internal sealed record Success : RemoveResult;
        internal sealed record NotFound : RemoveResult;
        internal sealed record Conflict : RemoveResult;
    }
}

public sealed record RemoveNoteInput(
    [property: JsonRequired, Uuid] string Id,
    [property: JsonRequired, Range(1, long.MaxValue, ErrorMessage = "版は1以上で指定してください。")] long Version);
