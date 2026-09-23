using NotesSample.Infrastructure.Configuration;
using NotesSample.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Xunit;

namespace NotesSample.Tests;

public sealed class OpenApiTests
{
    [Fact]
    public async Task ContractGenerationIncludesSaveWithoutCreatingDatabaseOrStartingServerAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"aidd-openapi-{Guid.NewGuid():N}");
        var config = AppConfig.FromValues(key => key == "DB_PATH" ? Path.Combine(directory, "app.sqlite") : null);
        await using var app = await AppHost.BuildAppAsync(config, initializeDatabase: false);
        var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);

        document.Paths.ContainsKey("/api/notes/save").ShouldBeTrue();
        document.Components!.Schemas!.ContainsKey("SaveNoteRequest").ShouldBeTrue();
        document.Components.Schemas.ContainsKey("SaveNoteResponse").ShouldBeTrue();
        var input = document.Components.Schemas["SaveNoteRequest"];
        input.Required!.Contains("title").ShouldBeTrue();
        input.Required.Contains("body").ShouldBeTrue();
        input.Required.Contains("id").ShouldBeFalse();
        input.Required.Contains("version").ShouldBeFalse();
        input.Properties!["id"].Type.ShouldBe(JsonSchemaType.String);
        input.Properties["version"].Type.ShouldBe(JsonSchemaType.Integer);
        Directory.Exists(directory).ShouldBeFalse();
        app.Lifetime.ApplicationStarted.IsCancellationRequested.ShouldBeFalse();
    }
}
