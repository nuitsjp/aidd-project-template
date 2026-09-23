using NotesSample.Application;
using NotesSample.Application.Authentication;
using NotesSample.Domain.Notes;
using NotesSample.Infrastructure.Authentication;
using NotesSample.Infrastructure.Notifications;
using NotesSample.Infrastructure.Persistence;
using NotesSample.Presentation.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace NotesSample.Features.Notes;

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
            InsertAsync = NotePersistence.InsertAsync;
            TranslateError = NotePersistence.TranslateError;
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
    }
}

internal sealed record BulkResult(int Count);
