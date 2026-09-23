using NotesSample.Infrastructure.Persistence;
using NotesSample.Infrastructure.Notifications;
using NotesSample.Features.Notes;

namespace NotesSample.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"aidd-dotnet-{Guid.NewGuid():N}");

    private TestDatabase()
    {
        Path = System.IO.Path.Combine(directory, "app.sqlite");
        Database = new Database(Path);
        Notifications = new ChangeNotifications(NotificationErrors.Add);
        ListNotes = new ListNotes(Database);
        GetNote = new GetNote(Database);
        RemoveNote = new RemoveNote(Database, Notifications);
        PreviewNotes = new PreviewNotes();
        ImportNotes = new ImportNotes(Database, Notifications, PreviewNotes.Application);
        Save = new SaveNote(Database, Notifications);
    }

    internal static async Task<TestDatabase> CreateAsync()
    {
        var fixture = new TestDatabase();
        await fixture.Database.InitializeAsync();
        await fixture.EnsureUserAsync("alice", "Alice");
        await fixture.EnsureUserAsync("bob", "Bob");
        return fixture;
    }

    internal string Path { get; }

    internal Database Database { get; }

    internal ChangeNotifications Notifications { get; }

    internal ListNotes ListNotes { get; set; }

    internal GetNote GetNote { get; set; }

    internal RemoveNote RemoveNote { get; set; }

    internal PreviewNotes PreviewNotes { get; set; }

    internal ImportNotes ImportNotes { get; set; }

    internal SaveNote Save { get; set; }

    internal List<Exception> NotificationErrors { get; } = [];

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    private async Task EnsureUserAsync(string id, string name)
    {
        await using var connection = await Database.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (id, name)
            VALUES
                ($id, $name)
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        await command.ExecuteNonQueryAsync();
    }
}
