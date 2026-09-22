using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;
using Aidd.ReactDotnet.Presentation.Http;
using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Infrastructure.Configuration;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Domain;
using System.Net;
using System.Text.Json;
using Aidd.ReactDotnet.Features.Notes;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Aidd.ReactDotnet;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["openapi", var output])
            {
                await using var schemaApp = BuildApp(AppConfig.FromValues(_ => null), initializeDatabase: false);
                var provider = schemaApp.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
                var document = await provider.GetOpenApiDocumentAsync();
                await using var stream = File.Create(output);
                await document.SerializeAsJsonAsync(stream, OpenApiSpecVersion.OpenApi3_0);
                return 0;
            }

            if (args.Length > 0)
            {
                return RunCommand(args);
            }

            var config = AppConfig.FromEnvironment();
            await using var app = BuildApp(config);
            await app.StartAsync();
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
            var url = addresses?.Addresses.SingleOrDefault()
                ?? throw new InvalidOperationException("起動URLを取得できませんでした。");
            Console.WriteLine("AIDD_READY " + JsonSerializer.Serialize(new { url }));
            Console.Out.Flush();

            if (Environment.GetEnvironmentVariable("AIDD_CONTROL_STDIN") == "1")
            {
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

    internal static WebApplication BuildApp(AppConfig config, bool initializeDatabase = true)
    {
        var database = new Database(config.DatabasePath);
        if (initializeDatabase) database.Initialize();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = config.WebRootPath,
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 1024 * 1024;
            options.Listen(config.BindAddress, config.Port);
        });
        builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(10));
        builder.Services.AddProblemDetails();
        builder.Services.AddValidation();
        builder.Services.AddOpenApi(options =>
        {
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
        if (!initializeDatabase)
            builder.Services.AddSingleton<EndpointDataSource>(_ => new CompositeEndpointDataSource(contractSources));
        var app = builder.Build();
        var notifications = new ChangeNotifications(error => app.Logger.LogWarning(error, "変更通知に失敗しました"));
        var notes = new NotesService(database, notifications);
        var identity = new IdentityService(database, config.AuthMode);

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
            try
            {
                await next(context);
                // 型付きバインディングが例外を投げずに返すサイズ超過等も公開エラー形式に統一する。
                if (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted
                    && context.Response.ContentType is null
                    && context.Response.StatusCode is 400 or 413 or 415)
                {
                    if (context.Response.StatusCode == StatusCodes.Status400BadRequest)
                        await WriteValidationProblemAsync(context, "入力の形式を確認してください。");
                    else
                        await WriteProblemAsync(context, context.Response.StatusCode,
                            context.Response.StatusCode == 413 ? "リクエストが大きすぎます。" : "入力の形式を確認してください。");
                }
            }
            catch (AppFaultException fault)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                var status = fault.Code switch
                {
                    "UNAUTHENTICATED" => StatusCodes.Status401Unauthorized,
                    "NOT_FOUND" => StatusCodes.Status404NotFound,
                    "EDIT_CONFLICT" or "TITLE_EXISTS" => StatusCodes.Status409Conflict,
                    "VALIDATION" => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError,
                };
                if (status == StatusCodes.Status400BadRequest)
                    await WriteValidationProblemAsync(context, fault.Message);
                else
                    await WriteProblemAsync(context, status, fault.Message);
            }
            catch (BadHttpRequestException error) when (error.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status413PayloadTooLarge or StatusCodes.Status415UnsupportedMediaType)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                if (error.StatusCode == StatusCodes.Status400BadRequest)
                    await WriteValidationProblemAsync(context, "入力の形式を確認してください。");
                else
                    await WriteProblemAsync(
                        context,
                        error.StatusCode,
                        error.StatusCode == 413 ? "リクエストが大きすぎます。" : "入力の形式を確認してください。");
            }
            catch (Exception error)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                app.Logger.LogError(error, "機能操作に失敗しました");
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status500InternalServerError,
                    "処理を完了できませんでした。");
            }
        });

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api") && context.Request.Path != "/events/notes")
            {
                await next(context);
                return;
            }

            context.Response.Headers.CacheControl = "no-store";
            var port = context.Connection.LocalPort;
            var localHost = config.Host == "::1" ? $"[::1]:{port}" : $"{config.Host}:{port}";
            var publicHost = config.PublicOrigin?.Authority;
            var requestHost = context.Request.Host.Value ?? string.Empty;
            if (!requestHost.Equals(localHost, StringComparison.OrdinalIgnoreCase) &&
                !requestHost.Equals($"localhost:{port}", StringComparison.OrdinalIgnoreCase) &&
                (publicHost is null || !requestHost.Equals(publicHost, StringComparison.OrdinalIgnoreCase)))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "接続先が不正です。");
                return;
            }

            var localOrigin = $"http://{localHost}";
            var expectedOrigin = config.PublicOrigin?.GetLeftPart(UriPartial.Authority) ?? localOrigin;
            var origin = context.Request.Headers.Origin.ToString();
            if ((!string.IsNullOrEmpty(origin) && origin != expectedOrigin && !config.AllowedOrigins.Contains(origin)) ||
                (HttpMethods.IsPost(context.Request.Method) && string.IsNullOrEmpty(origin)))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "同一サイトから操作してください。");
                return;
            }

            await next(context);
        });

        new SaveNote(database, notifications).Map(app, identity);
        ApiEndpoints.Map(app, config, identity, notes, notifications, app.Lifetime.ApplicationStopping);
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapFallback(async context =>
        {
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

            await WriteProblemAsync(
                context,
                StatusCodes.Status404NotFound,
                "見つかりません。");
        });
        if (!initializeDatabase)
            contractSources.AddRange(((IEndpointRouteBuilder)app).DataSources);
        return app;
    }

    private static int RunCommand(string[] args)
    {
        if (args is ["db:backup", var destination])
        {
            new Database(AppConfig.DatabasePathFromEnvironment()).Backup(destination);
            Console.WriteLine("整合したバックアップを作成しました。");
            return 0;
        }

        if (args is ["db:check", var path])
        {
            var result = new Database(path).Check();
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

    private static Task WriteProblemAsync(HttpContext context, int status, string detail) =>
        Results.Problem(
            statusCode: status,
            title: status switch
            {
                StatusCodes.Status401Unauthorized => "認証が必要です。",
                StatusCodes.Status403Forbidden => "操作できません。",
                StatusCodes.Status404NotFound => "見つかりません。",
                StatusCodes.Status409Conflict => "競合が発生しました。",
                StatusCodes.Status413PayloadTooLarge => "リクエストが大きすぎます。",
                StatusCodes.Status415UnsupportedMediaType => "対応していない形式です。",
                _ => "処理を完了できませんでした。",
            },
            detail: detail)
        .ExecuteAsync(context);

    private static Task WriteValidationProblemAsync(HttpContext context, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]> { ["request"] = [message] },
            title: "入力内容を確認してください。")
        .ExecuteAsync(context);
}
