using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Features.Notes;

namespace Aidd.ReactDotnet.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"aidd-dotnet-{Guid.NewGuid():N}");

    internal TestDatabase()
    {
        Path = System.IO.Path.Combine(directory, "app.sqlite");
        Database = new Database(Path);
        Database.Initialize();
        EnsureUser("alice", "Alice");
        EnsureUser("bob", "Bob");
        Notifications = new ChangeNotifications(NotificationErrors.Add);
        Notes = new NotesService(Database, Notifications);
        Save = new SaveNote(Database, Notifications);
    }

    internal string Path { get; }

    internal Database Database { get; }

    internal ChangeNotifications Notifications { get; }

    internal NotesService Notes { get; set; }

    internal SaveNote Save { get; set; }

    internal List<Exception> NotificationErrors { get; } = [];

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    private void EnsureUser(string id, string name)
    {
        using var connection = Database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO users(id,name) VALUES($id,$name)";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();
    }
}

internal static class TestAssert
{
    internal static T Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T error)
        {
            return error;
        }

        Assert.Fail($"{typeof(T).Name} が発生しませんでした。");
        throw new InvalidOperationException();
    }

    internal static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try
        {
            await action();
        }
        catch (T error)
        {
            return error;
        }

        Assert.Fail($"{typeof(T).Name} が発生しませんでした。");
        throw new InvalidOperationException();
    }
}
