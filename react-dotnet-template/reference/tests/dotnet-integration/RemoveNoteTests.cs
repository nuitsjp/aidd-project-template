using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using NotesSample.Application.Authentication;
using NotesSample.Features.Notes;
using NotesSample.Infrastructure.Notifications;
using NotesSample.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace NotesSample.IntegrationTests;

public sealed class RemoveNoteTests
{
    private static readonly Principal Alice = new("alice", "Alice");
    private static readonly Principal Bob = new("bob", "Bob");
    private const string NoteId = "00000000-0000-4000-8000-000000000001";

    [Fact]
    public async Task MatchingVersion_DeletesNoteAndPublishesChangeAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        await fixture.InsertNoteAsync();
        var notifications = 0;
        var remainingWhenNotified = -1;
        using var subscription = fixture.Notifications.Subscribe("alice", () =>
        {
            notifications++;
            using var connection = new SqliteConnection($"Data Source={fixture.Path};Mode=ReadOnly;Pooling=False");
            connection.Open();
            remainingWhenNotified = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM notes WHERE id = @Id", new { Id = NoteId });
        });

        var result = await fixture.RemoveNote.Presentation.ExecuteAsync(Alice, new RemoveNoteInput(NoteId, 1));

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(StatusCodes.Status200OK);
        (await fixture.CountNotesAsync()).ShouldBe(0);
        notifications.ShouldBe(1);
        remainingWhenNotified.ShouldBe(0);
    }

    [Fact]
    public async Task StaleVersion_ReturnsConflictAndKeepsNoteAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        await fixture.InsertNoteAsync();
        var notifications = 0;
        using var subscription = fixture.Notifications.Subscribe("alice", () => notifications++);

        var result = await fixture.RemoveNote.Presentation.ExecuteAsync(Alice, new RemoveNoteInput(NoteId, 2));

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        (await fixture.CountNotesAsync()).ShouldBe(1);
        notifications.ShouldBe(0);
    }

    [Fact]
    public async Task OtherOwner_ReturnsNotFoundAndKeepsNoteAsync()
    {
        using var fixture = await TestDatabase.CreateAsync();
        await fixture.InsertNoteAsync();

        var result = await fixture.RemoveNote.Presentation.ExecuteAsync(Bob, new RemoveNoteInput(NoteId, 1));

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        (await fixture.CountNotesAsync()).ShouldBe(1);
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"aidd-remove-note-{Guid.NewGuid():N}");

        private TestDatabase()
        {
            Path = System.IO.Path.Combine(directory, "app.sqlite");
            Database = new Database(Path);
            Notifications = new ChangeNotifications(_ => { });
            RemoveNote = new RemoveNote(Database, Notifications);
        }

        internal string Path { get; }
        private Database Database { get; }
        internal ChangeNotifications Notifications { get; }
        internal RemoveNote RemoveNote { get; }

        internal static async Task<TestDatabase> CreateAsync()
        {
            var fixture = new TestDatabase();
            await fixture.Database.InitializeAsync();
            await using var connection = await fixture.Database.OpenAsync();
            await connection.ExecuteAsync("INSERT INTO users (id, name) VALUES ('alice', 'Alice'), ('bob', 'Bob')");
            return fixture;
        }

        internal async Task InsertNoteAsync()
        {
            await using var connection = await Database.OpenAsync();
            await connection.ExecuteAsync("""
                INSERT INTO notes (id, owner_id, title, body, version, updated_at)
                VALUES (@Id, 'alice', 'target', 'body', 1, '2026-01-01T00:00:00Z')
                """, new { Id = NoteId });
        }

        internal async Task<int> CountNotesAsync()
        {
            await using var connection = await Database.OpenAsync();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM notes WHERE id = @Id", new { Id = NoteId });
        }

        public void Dispose() => Directory.Delete(directory, true);
    }
}
