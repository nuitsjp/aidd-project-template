using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace NotesSample.IntegrationTests.Http;

public sealed class HttpBoundaryTests
{
    private const string TitleError = "タイトルは1〜100文字で入力してください。";

    [Fact]
    public async Task SaveNote_ReturnsCamelCaseResultAndHidesNoteFromOtherUserAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        await server.SignInAsync("alice");

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        using var response = await server.PostJsonAsync("/api/notes/save", new { title = " 契約の確認 ", body = "本文" });
        var note = await ReadJsonAsync(response);
        var aliceNotes = await ReadJsonAsync(await server.GetAsync("/api/notes"));
        await server.SignInAsync("bob");
        using var other = await server.GetAsync($"/api/notes/{note.GetProperty("id").GetString()}");
        var bobNotes = await ReadJsonAsync(await server.GetAsync("/api/notes"));

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        note.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["body", "id", "title", "updatedAt", "version"]);
        note.GetProperty("title").GetString().ShouldBe("契約の確認");
        note.GetProperty("body").GetString().ShouldBe("本文");
        note.GetProperty("version").GetInt64().ShouldBe(1);
        Guid.TryParse(note.GetProperty("id").GetString(), out _).ShouldBeTrue();
        note.GetProperty("id").GetString()!.Length.ShouldBe(36);
        DateTimeOffset.TryParse(note.GetProperty("updatedAt").GetString(), out _).ShouldBeTrue();
        JsonNode.DeepEquals(JsonNode.Parse(aliceNotes.GetRawText()), new JsonArray(JsonNode.Parse(note.GetRawText()))).ShouldBeTrue();
        server.Rows().ShouldBe([new("alice", "契約の確認", "本文", 1)]);
        other.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var otherProblem = await ReadJsonAsync(other);
        otherProblem.GetProperty("status").GetInt32().ShouldBe(404);
        otherProblem.GetProperty("detail").GetString().ShouldBe("対象のメモが見つかりません。");
        bobNotes.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task SaveNote_ValidatesTitleLengthAfterTrimmingAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        await server.SignInAsync("alice");
        var longest = new string('あ', 100);

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        using var accepted = await server.PostJsonAsync("/api/notes/save", new { title = $"　{longest} ", body = "" });
        using var rejected = await server.PostJsonAsync("/api/notes/save", new { title = $" {longest}い ", body = "" });

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ErrorMessages(await ReadJsonAsync(rejected), "Title").ShouldBe([TitleError]);
        server.Rows().ShouldBe([new("alice", longest, "", 1)]);
    }

    [Fact]
    public async Task SaveNote_RejectsMalformedJsonAndInvalidShapesWithoutChangingDatabaseAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        await server.SignInAsync("alice");
        string[] invalidShapes =
        [
            "null",
            "{}",
            """{"title":"本文欠落"}""",
            """{"title":123,"body":""}""",
            """{"title":null,"body":""}""",
            """{"title":"null本文","body":null}""",
            """{"title":"明示null ID","body":"","id":null,"version":null}""",
            """{"title":"明示null 版","body":"","id":"00000000-0000-4000-8000-000000000001","version":null}""",
            """{"title":"明示null IDのみ","body":"","id":null,"version":1}""",
            """{"title":"余分な項目","body":"","ownerId":"bob"}""",
            """{"title":"大小文字","body":"","Title":"別名"}""",
            """{"title":"版文字列","body":"","id":"00000000-0000-4000-8000-000000000001","version":"1"}""",
            """{"title":"不正ID","body":"","id":"not-a-uuid","version":1}""",
            """{"title":"不正版","body":"","id":"00000000-0000-4000-8000-000000000001","version":0}""",
            """{"title":"版欠落","body":"","id":"00000000-0000-4000-8000-000000000001"}""",
            """{"title":"ID欠落","body":"","version":1}""",
        ];

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        var shapeResponses = new List<HttpResponseMessage>();
        foreach (var input in invalidShapes)
            shapeResponses.Add(await PostRawJsonAsync(server, input));
        using var duplicate = await PostRawJsonAsync(server, """{"title":"重複キー","body":"","title":"重複キー"}""");
        using var multiple = await server.PostJsonAsync("/api/notes/save", new { title = "   ", body = new string('x', 10_001) });
        using var broken = await PostRawJsonAsync(server, "{");
        using var oversized = await server.PostJsonAsync("/api/notes/save", new { title = "上限超過", body = new string('x', 1024 * 1024) });

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        foreach (var (input, response) in invalidShapes.Zip(shapeResponses))
        {
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, input);
                ShouldBeValidationProblem(await ReadJsonAsync(response), input);
            }
        }
        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldBeValidationProblem(await ReadJsonAsync(duplicate));
        multiple.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        multiple.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var multipleProblem = await ReadJsonAsync(multiple);
        multipleProblem.GetProperty("status").GetInt32().ShouldBe(400);
        ErrorMessages(multipleProblem, "Title").ShouldBe([TitleError]);
        ErrorMessages(multipleProblem, "Body").ShouldBe(["本文は10,000文字以内で入力してください。"]);
        broken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ShouldBeValidationProblem(await ReadJsonAsync(broken));
        oversized.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        var oversizedProblem = await ReadJsonAsync(oversized);
        oversizedProblem.GetProperty("status").GetInt32().ShouldBe(413);
        oversizedProblem.GetProperty("detail").GetString().ShouldBe("リクエストが大きすぎます。");
        server.Rows().ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveNote_RejectsInvalidIdAndVersionWithoutChangingDatabaseAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        await server.SignInAsync("alice");
        using var saved = await server.PostJsonAsync("/api/notes/save", new { title = "削除しないメモ", body = "本文" });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var note = await ReadJsonAsync(saved);
        var id = note.GetProperty("id").GetString();
        var version = note.GetProperty("version").GetInt64();
        (object Input, string Field)[] cases =
        [
            (new { id = "not-a-uuid", version }, "Id"),
            (new { id, version = 0 }, "Version"),
        ];

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        var responses = new List<HttpResponseMessage>();
        foreach (var (input, _) in cases)
            responses.Add(await server.PostJsonAsync("/api/notes/remove", input));

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        foreach (var ((input, field), response) in cases.Zip(responses))
        {
            using (response)
            {
                var message = JsonSerializer.Serialize(input);
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, message);
                var problem = await ReadJsonAsync(response);
                problem.GetProperty("status").GetInt32().ShouldBe(400, message);
                problem.GetProperty("errors").GetProperty(field).ValueKind.ShouldBe(JsonValueKind.Array, message);
            }
        }
        server.Rows().ShouldBe([new("alice", "削除しないメモ", "本文", 1)]);
    }

    [Fact]
    public async Task Api_RejectsUnauthenticatedAndCrossOriginRequestsAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        var input = new { title = "保存しない", body = "" };

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        var session = await ReadJsonAsync(await server.GetAsync("/api/session"));
        using var anonymous = await server.GetAsync("/api/notes");
        await server.SignInAsync("alice");
        using var foreign = await server.SendAsync(HttpMethod.Post, "/api/notes/save", JsonContent.Create(input), "https://other.example");
        using var missingOrigin = await server.SendAsync(HttpMethod.Post, "/api/notes/save", JsonContent.Create(input), origin: null);

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        JsonNode.DeepEquals(JsonNode.Parse(session.GetRawText()), new JsonObject { ["user"] = null, ["mode"] = "demo" }).ShouldBeTrue();
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var anonymousProblem = await ReadJsonAsync(anonymous);
        anonymousProblem.GetProperty("status").GetInt32().ShouldBe(401);
        anonymousProblem.GetProperty("detail").GetString().ShouldBe("利用者を確認できません。");
        foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        missingOrigin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        server.Rows().ShouldBeEmpty();
    }

    [Fact]
    public async Task Api_RejectsUnknownHostNameAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        var port = server.Url.Port;

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        using var foreignHost = await GetWithHostAsync(server, $"other.example:{port}");
        using var localhost = await GetWithHostAsync(server, $"localhost:{port}");

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        foreignHost.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await ReadJsonAsync(foreignHost);
        problem.GetProperty("status").GetInt32().ShouldBe(403);
        problem.GetProperty("detail").GetString().ShouldBe("接続先が不正です。");
        localhost.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TestResetEndpoint_IsNotExposedAsync()
    {
        // -------------------------------------------------------------
        // Arrange
        // -------------------------------------------------------------
        await using var server = await TestAppServer.StartAsync();
        await server.SignInAsync("alice");

        // -------------------------------------------------------------
        // Act
        // -------------------------------------------------------------
        using var response = await server.SendAsync(HttpMethod.Post, "/api/test/reset", content: null, server.Origin);

        // -------------------------------------------------------------
        // Assert
        // -------------------------------------------------------------
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> PostRawJsonAsync(TestAppServer server, string json) =>
        server.SendAsync(HttpMethod.Post, "/api/notes/save", new StringContent(json, Encoding.UTF8, "application/json"), server.Origin);

    private static async Task<HttpResponseMessage> GetWithHostAsync(TestAppServer server, string host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/session");
        request.Headers.Host = host;
        return await server.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }

    private static void ShouldBeValidationProblem(JsonElement problem, string? customMessage = null)
    {
        problem.GetProperty("status").GetInt32().ShouldBe(400, customMessage);
        problem.GetProperty("errors").ValueKind.ShouldBe(JsonValueKind.Object, customMessage);
    }

    private static string?[] ErrorMessages(JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(message => message.GetString()).ToArray();
}
