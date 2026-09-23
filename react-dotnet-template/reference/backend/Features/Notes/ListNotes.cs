using Aidd.ReactDotnet.Application;
using Aidd.ReactDotnet.Application.Authentication;
using Aidd.ReactDotnet.Domain;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Presentation.Http;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed class ListNotes
{
    internal ListNotes(Database database)
    {
        Presentation = new PresentationLayer(new ApplicationLayer(database));
    }

    internal PresentationLayer Presentation { get; }

    internal void Map(WebApplication app, IdentityService identity)
    {
        app.MapGet("/api/notes", async (HttpRequest request) =>
        {
            var principal = identity.Resolve(request)
                ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");
            return TypedResults.Ok(await Presentation.ExecuteAsync(principal));
        });
    }

    internal sealed class PresentationLayer(IApplicationLayer<ListNotesRequest, IReadOnlyList<Note>> application)
    {
        internal Func<Principal, Task<IReadOnlyList<Note>>> ExecuteAsync { get; set; } =
            principal => application.ExecuteAsync(principal, new ListNotesRequest());
    }

    internal sealed class ApplicationLayer(Database database)
        : IApplicationLayer<ListNotesRequest, IReadOnlyList<Note>>
    {
        internal Func<SqliteConnection, string, Task<IReadOnlyList<Note>>> ReadAllAsync { get; set; } =
            PersistenceLayer.ReadAllAsync;

        public async Task<IReadOnlyList<Note>> ExecuteAsync(Principal principal, ListNotesRequest input)
        {
            await using var connection = await database.OpenAsync();
            return await ReadAllAsync(connection, principal.Id);
        }
    }

    internal static class PersistenceLayer
    {
        internal static async Task<IReadOnlyList<Note>> ReadAllAsync(SqliteConnection connection, string ownerId) =>
            (await connection.QueryAsync<Note>(
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
                    owner_id = @ownerId
                ORDER BY
                    updated_at DESC, id
                """,
                new { ownerId })).AsList();
    }
}

internal sealed record ListNotesRequest;
