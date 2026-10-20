using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NotesSample.Hosting;
using NotesSample.Infrastructure.Configuration;
using Xunit;

namespace NotesSample.IntegrationTests.Http;

/// <summary>
/// 本番と同じKestrelをloopbackの空きポートで起動し、一時SQLiteとdemo認証で公開境界へ直接接続する。
/// </summary>
internal sealed class TestAppServer : IAsyncDisposable
{
    private readonly TestSqliteDatabase database;
    private readonly WebApplication app;

    private TestAppServer(TestSqliteDatabase database, WebApplication app, Uri url)
    {
        this.database = database;
        this.app = app;
        Url = url;
        Origin = url.GetLeftPart(UriPartial.Authority);
        Client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
        {
            BaseAddress = url,
        };
    }

    internal Uri Url { get; }
    internal string Origin { get; }
    internal HttpClient Client { get; }

    internal static async Task<TestAppServer> StartAsync()
    {
        var database = await TestSqliteDatabase.CreateAsync();
        try
        {
            var values = new Dictionary<string, string?>
            {
                ["HOST"] = "127.0.0.1",
                ["PORT"] = "0",
                ["DB_PATH"] = database.Path,
                ["AUTH_MODE"] = "demo",
            };
            var app = await AppHost.BuildAppAsync(AppConfig.FromValues(name => values.GetValueOrDefault(name)));
            await app.StartAsync(TestContext.Current.CancellationToken);
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new TestAppServer(database, app, new Uri(address));
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 書込み用の既定のOriginを付けて送る。
    /// </summary>
    internal Task<HttpResponseMessage> PostJsonAsync<T>(string path, T value) =>
        SendAsync(HttpMethod.Post, path, JsonContent.Create(value), Origin);

    internal async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, string? origin)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (origin is not null) request.Headers.Add("Origin", origin);
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    internal Task<HttpResponseMessage> GetAsync(string path) =>
        Client.GetAsync(path, TestContext.Current.CancellationToken);

    internal async Task SignInAsync(string user)
    {
        using var response = await PostJsonAsync("/api/demo/sign-in", new { user });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// HTTPの結果を再利用せず、別の読取専用接続から確定済みのデータを読む。
    /// </summary>
    internal IReadOnlyList<NoteRow> Rows()
    {
        using var connection = new SqliteConnection($"Data Source={database.Path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        return connection.Query<NoteRow>(
            "SELECT owner_id AS OwnerId, title AS Title, body AS Body, version AS Version FROM notes ORDER BY title").ToList();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        try
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
        finally
        {
            database.Dispose();
        }
    }

    internal sealed record NoteRow(string OwnerId, string Title, string Body, long Version);
}
