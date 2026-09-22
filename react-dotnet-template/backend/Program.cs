using System.Net;
using System.Text.Json;
using Aidd.ReactDotnet.Features.Notes;
using Aidd.ReactDotnet.Http;
using Aidd.ReactDotnet.Shared;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Aidd.ReactDotnet;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
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
                _ = ReadControlInput(app.Lifetime);
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

    internal static WebApplication BuildApp(AppConfig config)
    {
        AppDatabase.Initialize(config.DatabasePath);
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
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var app = builder.Build();
        var notifications = new ChangeNotifications(error => app.Logger.LogWarning(error, "変更通知に失敗しました"));
        var notes = new NotesService(config.DatabasePath, notifications, error => app.Logger.LogWarning(error, "変更通知に失敗しました"));
        var identity = new IdentityService(config.DatabasePath, config.AuthMode);

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
            try
            {
                await next(context);
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
                await WriteError(context, status, new PublicAppError(fault.Code, fault.Message, fault.FieldErrors));
            }
            catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                await WriteError(
                    context,
                    StatusCodes.Status413PayloadTooLarge,
                    new PublicAppError("VALIDATION", "リクエストが大きすぎます。"));
            }
            catch (Exception error)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                app.Logger.LogError(error, "機能操作に失敗しました");
                await WriteError(
                    context,
                    StatusCodes.Status500InternalServerError,
                    new PublicAppError("INTERNAL", "処理を完了できませんでした。"));
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
                await WriteError(
                    context,
                    StatusCodes.Status403Forbidden,
                    new PublicAppError("VALIDATION", "接続先が不正です。"));
                return;
            }

            var localOrigin = $"http://{localHost}";
            var expectedOrigin = config.PublicOrigin?.GetLeftPart(UriPartial.Authority) ?? localOrigin;
            var origin = context.Request.Headers.Origin.ToString();
            if ((!string.IsNullOrEmpty(origin) && origin != expectedOrigin && !config.AllowedOrigins.Contains(origin)) ||
                (HttpMethods.IsPost(context.Request.Method) && string.IsNullOrEmpty(origin)))
            {
                await WriteError(
                    context,
                    StatusCodes.Status403Forbidden,
                    new PublicAppError("VALIDATION", "同一サイトから操作してください。"));
                return;
            }

            await next(context);
        });

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

            await WriteError(
                context,
                StatusCodes.Status404NotFound,
                new PublicAppError("NOT_FOUND", "見つかりません。"));
        });
        return app;
    }

    private static int RunCommand(string[] args)
    {
        if (args is ["db:backup", var destination])
        {
            AppDatabase.Backup(AppConfig.DatabasePathFromEnvironment(), destination);
            Console.WriteLine("整合したバックアップを作成しました。");
            return 0;
        }

        if (args is ["db:check", var path])
        {
            var result = AppDatabase.Check(path);
            Console.WriteLine(JsonSerializer.Serialize(new { version = result.Version, result = result.Result }));
            return result.IsHealthy ? 0 : 1;
        }

        throw new InvalidOperationException("使用方法: App db:backup <新規ファイル> | App db:check <DBファイル>");
    }

    private static Task ReadControlInput(IHostApplicationLifetime lifetime) => Task.Run(async () =>
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

    private static async Task WriteError(HttpContext context, int status, PublicAppError error)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new AppErrorEnvelope(error));
    }
}
