using NotesSample.Application;
using NotesSample.Application.Authentication;
using NotesSample.Domain;
using NotesSample.Domain.Notes;
using NotesSample.Infrastructure.Authentication;
using NotesSample.Infrastructure.Persistence;
using NotesSample.Presentation.Http;
using Dapper;
using Microsoft.Data.Sqlite;

namespace NotesSample.Features.Notes;

internal sealed class GetNote
{
    internal GetNote(Database database)
    {
        Presentation = new PresentationLayer(new ApplicationLayer(database));
    }

    internal PresentationLayer Presentation { get; }

    internal void Map(WebApplication app, IdentityService identity)
    {
        app.MapGet("/api/notes/{id}", async (string id, HttpRequest request) =>
        {
            if (!JsonRequest.IsUuid(id))
            {
                throw AppFaultException.Validation();
            }

            var principal = identity.Resolve(request)
                ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");
            return TypedResults.Ok(await Presentation.ExecuteAsync(principal, id));
        });
    }

    internal sealed class PresentationLayer(IApplicationLayer<string, Note> application)
    {
        internal Func<Principal, string, Task<Note>> ExecuteAsync { get; set; } = application.ExecuteAsync;
    }

    internal sealed class ApplicationLayer(Database database) : IApplicationLayer<string, Note>
    {
        internal Func<SqliteConnection, string, string, Task<Note?>> ReadAsync { get; set; } = PersistenceLayer.ReadAsync;

        public async Task<Note> ExecuteAsync(Principal principal, string id)
        {
            await using var connection = await database.OpenAsync();
            return await ReadAsync(connection, principal.Id, id)
                ?? throw new AppFaultException("NOT_FOUND", "対象のメモが見つかりません。");
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
    }
}
