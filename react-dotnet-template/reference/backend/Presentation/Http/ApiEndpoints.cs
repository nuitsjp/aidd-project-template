using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Infrastructure.Configuration;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Application.Authentication;
using Aidd.ReactDotnet.Domain;
using System.Threading.Channels;

namespace Aidd.ReactDotnet.Presentation.Http;

internal static class ApiEndpoints
{
    internal static void MapApplicationEndpoints(WebApplication app, AppConfig config, IdentityService identity)
    {
        app.MapGet("/api/session", (HttpRequest request) =>
            TypedResults.Ok(new SessionOutput(identity.Resolve(request), config.AuthMode)));

        if (config.AuthMode == "demo")
        {
            app.MapAnonymousPost<DemoSignInInput, SignInOutput>(
                "/api/demo/sign-in",
                "DemoSignIn",
                (context, input) => Task.FromResult(
                    new SignInOutput(identity.SignIn(context.Request, context.Response, input.User))));
            app.MapPost("/api/demo/sign-out", (HttpContext context) =>
            {
                identity.SignOut(context.Request, context.Response);
                return TypedResults.Ok(new SuccessOutput(true));
            });
        }
    }

    internal static void MapEventEndpoints(
        WebApplication app,
        IdentityService identity,
        ChangeNotifications notifications,
        CancellationToken applicationStopping)
    {
        app.MapGet("/events/notes", async (HttpContext context) =>
        {
            var user = RequireUser(identity, context.Request);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                applicationStopping);
            var cancellationToken = stopping.Token;
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Connection = "keep-alive";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            var pending = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
            using var subscription = notifications.Subscribe(user.Id, () => pending.Writer.TryWrite(true));
            using var keepalive = new Timer(_ => pending.Writer.TryWrite(false), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
            try
            {
                await WriteEventAsync(context.Response, "ready", cancellationToken);
                while (await pending.Reader.WaitToReadAsync(cancellationToken))
                {
                    var changed = false;
                    while (pending.Reader.TryRead(out var item))
                    {
                        changed |= item;
                    }

                    if (changed)
                    {
                        await WriteEventAsync(context.Response, "notes.changed", cancellationToken);
                    }
                    else
                    {
                        await context.Response.WriteAsync(": keepalive\n\n", cancellationToken);
                        await context.Response.Body.FlushAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        });

        app.MapGet("/health", () => TypedResults.Ok(new HealthOutput("ok")));
    }

    private static Principal RequireUser(IdentityService identity, HttpRequest request) =>
        identity.Resolve(request) ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");

    private static async Task WriteEventAsync(HttpResponse response, string eventName, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventName}\ndata: {{}}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
