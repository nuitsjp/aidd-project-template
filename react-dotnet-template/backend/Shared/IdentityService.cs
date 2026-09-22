using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Aidd.ReactDotnet.Shared;

internal sealed partial class IdentityService
{
    private const string CookieName = "aidd_session";
    private static readonly FrozenDictionary<string, string> DemoUsers =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["alice"] = "Alice", ["bob"] = "Bob" }
            .ToFrozenDictionary(StringComparer.Ordinal);
    private readonly string databasePath;
    private readonly string authMode;
    private readonly ConcurrentDictionary<string, Session> sessions = new(StringComparer.Ordinal);

    internal IdentityService(string databasePath, string authMode)
    {
        this.databasePath = databasePath;
        this.authMode = authMode;
    }

    internal Principal? Resolve(HttpRequest request)
    {
        if (authMode == "proxy")
        {
            var id = request.Headers["X-Authenticated-User"].ToString();
            return AuthenticatedUserPattern().IsMatch(id) ? EnsureUser(new Principal(id, id)) : null;
        }

        if (!request.Cookies.TryGetValue(CookieName, out var key) || !sessions.TryGetValue(key, out var session))
        {
            return null;
        }

        if (session.Expires <= DateTimeOffset.UtcNow)
        {
            sessions.TryRemove(key, out _);
            return null;
        }

        return session.User;
    }

    internal Principal SignIn(HttpRequest request, HttpResponse response, string id)
    {
        if (authMode != "demo" || !DemoUsers.TryGetValue(id, out var name))
        {
            throw AppFaultException.Validation("参照用ユーザーが不正です。");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            if (session.Value.Expires <= now)
            {
                sessions.TryRemove(session.Key, out _);
            }
        }

        if (request.Cookies.TryGetValue(CookieName, out var previous))
        {
            sessions.TryRemove(previous, out _);
        }

        if (sessions.Count >= 100)
        {
            throw new AppFaultException("VALIDATION", "参照用セッションの上限です。");
        }

        var user = EnsureUser(new Principal(id, name));
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        sessions[token] = new Session(user, now.AddHours(8));
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = false,
            Path = "/",
            MaxAge = TimeSpan.FromHours(8),
        });
        return user;
    }

    internal void SignOut(HttpRequest request, HttpResponse response)
    {
        if (request.Cookies.TryGetValue(CookieName, out var key))
        {
            sessions.TryRemove(key, out _);
        }

        response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
    }

    internal Principal EnsureUser(Principal user)
    {
        using var connection = AppDatabase.OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users(id,name) VALUES($id,$name)
            ON CONFLICT(id) DO UPDATE SET name=excluded.name WHERE users.name<>excluded.name
            """;
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$name", user.Name);
        command.ExecuteNonQuery();
        return user;
    }

    [GeneratedRegex("^[a-zA-Z0-9@._:+/-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex AuthenticatedUserPattern();

    private sealed record Session(Principal User, DateTimeOffset Expires);
}
