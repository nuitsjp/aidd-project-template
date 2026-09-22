using System.Threading.Channels;
using Aidd.ReactDotnet.Features.Notes;
using Aidd.ReactDotnet.Shared;

namespace Aidd.ReactDotnet.Http;

internal static class ApiEndpoints
{
    internal static void Map(
        WebApplication app,
        AppConfig config,
        IdentityService identity,
        NotesService notes,
        ChangeNotifications notifications,
        CancellationToken applicationStopping)
    {
        app.MapGet("/api/session", (HttpRequest request) =>
            Results.Ok(new { user = identity.Resolve(request), mode = config.AuthMode }));

        if (config.AuthMode == "demo")
        {
            app.MapPost("/api/demo/sign-in", async (HttpContext context) =>
            {
                var id = await JsonInput.ReadDemoUser(context.Request, context.RequestAborted);
                return Results.Ok(new { user = identity.SignIn(context.Request, context.Response, id) });
            });
            app.MapPost("/api/demo/sign-out", (HttpContext context) =>
            {
                identity.SignOut(context.Request, context.Response);
                return Results.Ok(new { ok = true });
            });
        }

        app.MapGet("/api/notes", (HttpRequest request) => Results.Ok(notes.List(RequireUser(identity, request).Id)));
        app.MapGet("/api/notes/{id}", (string id, HttpRequest request) =>
        {
            if (!JsonInput.IsUuid(id))
            {
                throw AppFaultException.Validation();
            }

            return Results.Ok(notes.Get(RequireUser(identity, request).Id, id));
        });
        app.MapPost("/api/notes/save", async (HttpContext context) =>
        {
            var user = RequireUser(identity, context.Request);
            var input = await JsonInput.ReadSaveNote(context.Request, context.RequestAborted);
            return Results.Ok(notes.Save(user.Id, input));
        });
        app.MapPost("/api/notes/remove", async (HttpContext context) =>
        {
            var user = RequireUser(identity, context.Request);
            var input = await JsonInput.ReadRemove(context.Request, context.RequestAborted);
            notes.Remove(user.Id, input.Id, input.Version);
            return Results.Ok(new { ok = true });
        });
        app.MapPost("/api/notes/preview", async (HttpContext context) =>
        {
            RequireUser(identity, context.Request);
            return Results.Ok(NotesService.Preview(await JsonInput.ReadBulk(context.Request, context.RequestAborted)));
        });
        app.MapPost("/api/notes/import", async (HttpContext context) =>
        {
            var user = RequireUser(identity, context.Request);
            return Results.Ok(notes.ImportMany(
                user.Id,
                await JsonInput.ReadBulk(context.Request, context.RequestAborted)));
        });

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
                await WriteEvent(context.Response, "ready", cancellationToken);
                while (await pending.Reader.WaitToReadAsync(cancellationToken))
                {
                    var changed = false;
                    while (pending.Reader.TryRead(out var item))
                    {
                        changed |= item;
                    }

                    if (changed)
                    {
                        await WriteEvent(context.Response, "notes.changed", cancellationToken);
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

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    }

    private static Principal RequireUser(IdentityService identity, HttpRequest request) =>
        identity.Resolve(request) ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");

    private static async Task WriteEvent(HttpResponse response, string eventName, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventName}\ndata: {{}}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
