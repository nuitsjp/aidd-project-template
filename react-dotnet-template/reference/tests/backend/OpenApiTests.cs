using Aidd.ReactDotnet.Infrastructure.Configuration;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Aidd.ReactDotnet.Tests;

[TestClass]
public sealed class OpenApiTests
{
    [TestMethod]
    public async Task ContractGenerationIncludesSaveWithoutCreatingDatabaseOrStartingServerAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-openapi-{Guid.NewGuid():N}");
        var config = AppConfig.FromValues(key => key == "DB_PATH" ? Path.Combine(directory, "app.sqlite") : null);
        await using var app = Program.BuildApp(config, initializeDatabase: false);
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync();

        Assert.IsTrue(document.Paths.ContainsKey("/api/notes/save"));
        Assert.IsTrue(document.Components!.Schemas!.ContainsKey("SaveNoteRequest"));
        Assert.IsTrue(document.Components.Schemas.ContainsKey("SaveNoteResponse"));
        var input = document.Components.Schemas["SaveNoteRequest"];
        Assert.IsTrue(input.Required!.Contains("title"));
        Assert.IsTrue(input.Required.Contains("body"));
        Assert.IsFalse(input.Required.Contains("id"));
        Assert.IsFalse(input.Required.Contains("version"));
        Assert.AreEqual(JsonSchemaType.String, input.Properties!["id"].Type);
        Assert.AreEqual(JsonSchemaType.Integer, input.Properties["version"].Type);
        Assert.IsFalse(Directory.Exists(directory));
        Assert.IsFalse(app.Lifetime.ApplicationStarted.IsCancellationRequested);
    }
}
