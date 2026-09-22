using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Infrastructure.Configuration;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Application.Authentication;
using Aidd.ReactDotnet.Domain;
using System.Threading.Channels;
using Aidd.ReactDotnet.Features.Notes;
using Microsoft.AspNetCore.Mvc;

namespace Aidd.ReactDotnet.Presentation.Http;

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

        app.MapGet("/api/notes", (HttpRequest request) => TypedResults.Ok(notes.List(RequireUser(identity, request).Id)));
        app.MapGet("/api/notes/{id}", (string id, HttpRequest request) =>
        {
            if (!JsonRequest.IsUuid(id))
            {
                throw AppFaultException.Validation();
            }

            return TypedResults.Ok(notes.Get(RequireUser(identity, request).Id, id));
        });
        app.MapAuthenticatedPost<RemoveNoteInput, SuccessOutput>(
            "/api/notes/remove",
            "RemoveNote",
            identity,
            (user, input) =>
            {
                if (!JsonRequest.IsUuid(input.Id) || input.Version <= 0) throw AppFaultException.Validation();
                notes.Remove(user.Id, input.Id, input.Version);
                return Task.FromResult(new SuccessOutput(true));
            })
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
        app.MapAuthenticatedPost<BulkInput, BulkPreview>(
            "/api/notes/preview",
            "PreviewNotes",
            identity,
            (_, input) => Task.FromResult(NotesService.Preview(input)));
        app.MapAuthenticatedPost<BulkInput, BulkResult>(
            "/api/notes/import",
            "ImportNotes",
            identity,
            (user, input) => Task.FromResult(notes.ImportMany(user.Id, input)))
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");

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

        app.MapGet("/health", () => TypedResults.Ok(new HealthOutput("ok")));
    }

    private static Principal RequireUser(IdentityService identity, HttpRequest request) =>
        identity.Resolve(request) ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");

    private static async Task WriteEvent(HttpResponse response, string eventName, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventName}\ndata: {{}}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
