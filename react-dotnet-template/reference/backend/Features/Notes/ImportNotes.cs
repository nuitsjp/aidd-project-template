using Aidd.ReactDotnet.Application;
using Aidd.ReactDotnet.Application.Authentication;
using Aidd.ReactDotnet.Domain;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Presentation.Http;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed class ImportNotes
{
    internal ImportNotes(
        Database database,
        ChangeNotifications notifications,
        IApplicationLayer<BulkInput, BulkPreview> preview)
    {
        Presentation = new PresentationLayer(new ApplicationLayer(database, notifications, preview));
    }

    internal PresentationLayer Presentation { get; }

    internal void Map(WebApplication app, IdentityService identity)
    {
        app.MapAuthenticatedPost<BulkInput, BulkResult>(
            "/api/notes/import",
            "ImportNotes",
            identity,
            Presentation.ExecuteAsync)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
    }

    internal sealed class PresentationLayer(IApplicationLayer<BulkInput, BulkResult> application)
    {
        internal Func<Principal, BulkInput, Task<BulkResult>> ExecuteAsync { get; set; } = application.ExecuteAsync;
    }

    internal sealed class ApplicationLayer : IApplicationLayer<BulkInput, BulkResult>
    {
        private readonly Database database;

        internal ApplicationLayer(
            Database database,
            ChangeNotifications notifications,
            IApplicationLayer<BulkInput, BulkPreview> preview)
        {
            this.database = database;
            PrepareAsync = preview.ExecuteAsync;
            InsertAsync = PersistenceLayer.InsertAsync;
            TranslateError = TranslateDatabaseError;
            PublishChange = notifications.Publish;
        }

        internal Func<Principal, BulkInput, Task<BulkPreview>> PrepareAsync { get; set; }
        internal Func<SqliteConnection, string, string, string, Task<Note>> InsertAsync { get; set; }
        internal Func<Exception, Exception> TranslateError { get; set; }
        internal Action<string> PublishChange { get; set; }

        public async Task<BulkResult> ExecuteAsync(Principal principal, BulkInput input)
        {
            var prepared = await PrepareAsync(principal, input);
            await using var transaction = await database.BeginTransactionAsync();
            try
            {
                foreach (var title in prepared.Titles)
                {
                    await InsertAsync(transaction.Connection, principal.Id, title, prepared.Body);
                }

                await transaction.CommitAsync();
            }
            catch (Exception error)
            {
                throw TranslateError(error);
            }

            PublishChange(principal.Id);
            return new BulkResult(prepared.Titles.Count);
        }

        internal static Exception TranslateDatabaseError(Exception error) =>
            error is SqliteException { SqliteExtendedErrorCode: 2067 }
                ? new AppFaultException("TITLE_EXISTS", "同じタイトルのメモが既にあります。")
                : error;
    }

    internal static class PersistenceLayer
    {
        internal static Task<Note> InsertAsync(SqliteConnection connection, string ownerId, string title, string body) =>
            connection.QuerySingleAsync<Note>(
                """
                INSERT INTO notes (id, owner_id, title, body, version, updated_at)
                VALUES
                    (@Id, @OwnerId, @Title, @Body, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                RETURNING
                    id AS Id,
                    title AS Title,
                    body AS Body,
                    version AS Version,
                    updated_at AS UpdatedAt
                """,
                new
                {
                    Id = Guid.NewGuid().ToString("D"),
                    OwnerId = ownerId,
                    Title = title,
                    Body = body,
                });
    }
}

internal sealed record BulkResult(int Count);
