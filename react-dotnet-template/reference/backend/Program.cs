using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;
using NotesSample.Presentation.Http;
using NotesSample.Infrastructure.Persistence;
using NotesSample.Infrastructure.Notifications;
using NotesSample.Infrastructure.Configuration;
using NotesSample.Infrastructure.Authentication;
using System.Text.Json;
using NotesSample.Features.Notes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace NotesSample;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // API契約の生成ではDBに触れず、実際のエンドポイント定義からスキーマを作る。
            if (args is ["openapi", var output])
            {
                await using var schemaApp = await BuildAppAsync(AppConfig.FromValues(_ => null), initializeDatabase: false);
                var provider = schemaApp.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
                var document = await provider.GetOpenApiDocumentAsync();
                await using var stream = File.Create(output);
                await document.SerializeAsJsonAsync(stream, OpenApiSpecVersion.OpenApi3_0);
                return 0;
            }

            if (args.Length > 0)
            {
                return await RunCommandAsync(args);
            }

            var config = AppConfig.FromEnvironment();
            await using var app = await BuildAppAsync(config);
            await app.StartAsync();
            // 開発・E2Eツールが動的に割り当てられた待受URLを取得できるよう通知する。
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var url = addresses?.Addresses.SingleOrDefault()
                ?? throw new InvalidOperationException("起動URLを取得できませんでした。");
            Console.WriteLine("AIDD_READY " + JsonSerializer.Serialize(new { url }));
            Console.Out.Flush();

            if (Environment.GetEnvironmentVariable("AIDD_CONTROL_STDIN") == "1")
            {
                // テスト用プロセスを標準入力から正常終了できるようにする。
                _ = ReadControlInputAsync(app.Lifetime);
            }

            await app.WaitForShutdownAsync();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    internal static async Task<WebApplication> BuildAppAsync(AppConfig config, bool initializeDatabase = true)
    {
        var database = new Database(config.DatabasePath);
        // 通常起動だけDBを初期化し、契約生成ではファイルを作らない。
        if (initializeDatabase) await database.InitializeAsync();
        // 配布先でも静的ファイルを実行ファイルの配置場所から解決する。
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = config.WebRootPath,
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            // APIの入力上限をサーバー入口で制限する。
            options.Limits.MaxRequestBodySize = 1024 * 1024;
            options.Listen(config.BindAddress, config.Port);
        });
        builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(10));
        builder.Services.AddProblemDetails();
        builder.Services.AddValidation();
        builder.Services.AddOpenApi(options =>
        {
            // 入れ子の契約型を区別し、null不可の指定を生成契約にも反映する。
            options.CreateSchemaReferenceId = type => type.Type.IsNested
                ? type.Type.DeclaringType!.Name + type.Type.Name
                : OpenApiOptions.CreateDefaultSchemaReferenceId(type);
            options.AddSchemaTransformer((schema, context, _) =>
            {
                foreach (var property in context.JsonTypeInfo.Type.GetProperties())
                {
                    if (property.SetMethod?.GetParameters().LastOrDefault()?.IsDefined(
                            typeof(System.Diagnostics.CodeAnalysis.DisallowNullAttribute), true) == true
                        && schema.Properties?.TryGetValue(JsonNamingPolicy.CamelCase.ConvertName(property.Name), out var propertySchema) == true
                        && propertySchema is OpenApiSchema value)
                        value.Type &= ~JsonSchemaType.Null;
                }
                return Task.CompletedTask;
            });
        });
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        // 未知の項目や曖昧なJSONを受け入れず、公開契約どおりにバインドする。
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.AllowDuplicateProperties = false;
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });

        var contractSources = new List<EndpointDataSource>();
        // DB未初期化の契約生成時にも、ビルド後に登録するエンドポイントを列挙できるようにする。
        if (!initializeDatabase)
            builder.Services.AddSingleton<EndpointDataSource>(_ => new CompositeEndpointDataSource(contractSources));
        var app = builder.Build();
        var notifications = new ChangeNotifications(error => app.Logger.LogWarning(error, "変更通知に失敗しました"));
        var identity = new IdentityService(database, config.AuthMode);

        app.UseHttpErrorHandling();
        app.UseApiRequestGuard(config);

        new SaveNote(database, notifications).Map(app, identity);
        ApiEndpoints.MapApplicationEndpoints(app, config, identity);
        new ListNotes(database).Map(app, identity);
        new GetNote(database).Map(app, identity);
        new RemoveNote(database, notifications).Map(app, identity);
        // 一括登録はプレビューと同じ入力準備処理を使う。
        var previewNotes = new PreviewNotes();
        previewNotes.Map(app, identity);
        new ImportNotes(database, notifications, previewNotes.Application).Map(app, identity);
        ApiEndpoints.MapEventEndpoints(app, identity, notifications, app.Lifetime.ApplicationStopping);
        // 配置済みReactを同じサーバーから配信し、画面遷移だけをindex.htmlへ戻す。
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapFallback(async context =>
        {
            // HTMLを要求する画面URLだけSPAに渡し、未知のAPIやイベントは404にする。
            if (HttpMethods.IsGet(context.Request.Method) &&
                !context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/events") &&
                context.Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "text/html") == true)
            {
                var index = Path.Combine(config.WebRootPath, "index.html");
                if (File.Exists(index))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.SendFileAsync(index, context.RequestAborted);
                    return;
                }
            }

            await ProblemResponses.WriteAsync(
                context,
                StatusCodes.Status404NotFound,
                "見つかりません。");
        });
        // 契約生成用プロバイダーに、Build後に登録したルートを渡す。
        if (!initializeDatabase)
            contractSources.AddRange(((IEndpointRouteBuilder)app).DataSources);
        return app;
    }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        if (args is ["db:backup", var destination])
        {
            new Database(AppConfig.DatabasePathFromEnvironment()).Backup(destination);
            Console.WriteLine("整合したバックアップを作成しました。");
            return 0;
        }

        if (args is ["db:check", var path])
        {
            var result = await new Database(path).CheckAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { version = result.Version, result = result.Result }));
            return result.IsHealthy ? 0 : 1;
        }

        throw new InvalidOperationException("使用方法: App db:backup <新規ファイル> | App db:check <DBファイル>");
    }

    private static Task ReadControlInputAsync(IHostApplicationLifetime lifetime) => Task.Run(async () =>
    {
        while (await Console.In.ReadLineAsync() is { } line)
        {
            if (line == "shutdown")
            {
                lifetime.StopApplication();
                return;
            }
        }
    });

}
