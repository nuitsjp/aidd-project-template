using Aidd.ReactDotnet.Presentation.Http;
using Aidd.ReactDotnet.Application;
using Aidd.ReactDotnet.Application.Authentication;
using Aidd.ReactDotnet.Infrastructure.Persistence;
using Aidd.ReactDotnet.Infrastructure.Notifications;
using Aidd.ReactDotnet.Infrastructure.Authentication;
using Aidd.ReactDotnet.Domain.Notes;
using Aidd.ReactDotnet.Domain;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed class SaveNote
{
    internal SaveNote(Database database, ChangeNotifications notifications)
    {
        Presentation = new PresentationLayer(
            new ApplicationLayer(database, notifications));
    }

    internal PresentationLayer Presentation { get; }

    internal void Map(WebApplication app, IdentityService identity)
    {
        app.MapPost("/api/notes/save", async (HttpContext context, SaveNoteRequest request) =>
        {
            var principal = identity.Resolve(context.Request)
                ?? throw new AppFaultException("UNAUTHENTICATED", "利用者を確認できません。");
            return await Presentation.HandleAsync(principal, request);
        })
        .WithName(nameof(SaveNote))
        .Produces<SaveNoteResponse>(StatusCodes.Status200OK)
        .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
        .ProducesCommonPostErrors();
    }

    internal sealed class PresentationLayer
    {
        internal PresentationLayer(IApplicationLayer<SaveNoteRequest, SaveResult> application)
        {
            ExecuteAsync = application.ExecuteAsync;
        }

        internal Func<Principal, SaveNoteRequest, Task<SaveResult>> ExecuteAsync { get; set; }

        internal async Task<IResult> HandleAsync(Principal principal, SaveNoteRequest request)
        {
            return await ExecuteAsync(principal, request) switch
            {
                SaveResult.Success success => TypedResults.Ok(success.Response),
                SaveResult.NotFound => TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "メモが見つかりません。",
                    detail: "対象のメモが見つかりません。"),
                SaveResult.Conflict => TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "更新が競合しました。",
                    detail: "別の操作で更新されています。下書きを保持したまま、最新版を確認してください。"),
                _ => throw new InvalidOperationException("未対応の保存結果です。"),
            };
        }
    }

    internal sealed class ApplicationLayer : IApplicationLayer<SaveNoteRequest, SaveResult>
    {
        private readonly Database database;

        internal ApplicationLayer(
            Database database,
            ChangeNotifications notifications)
        {
            this.database = database;
            ReadAsync = PersistenceLayer.ReadAsync;
            InsertAsync = PersistenceLayer.InsertAsync;
            UpdateAsync = PersistenceLayer.UpdateAsync;
            TranslateError = PersistenceLayer.TranslateError;
            PublishChange = notifications.Publish;
        }

        internal Func<SqliteConnection, string, string, Task<Note?>> ReadAsync { get; set; }
        internal Func<SqliteConnection, string, string, string, Task<Note>> InsertAsync { get; set; }
        internal Func<SqliteConnection, string, Note, Task<Note>> UpdateAsync { get; set; }
        internal Func<Exception, Exception> TranslateError { get; set; }
        internal Func<string, bool> PublishChange { get; set; }

        public async Task<SaveResult> ExecuteAsync(Principal principal, SaveNoteRequest input)
        {
            var ownerId = principal.Id;
            var title = input.Title.Trim();
            await using var transaction = await database.BeginTransactionAsync();
            Note saved;
            try
            {
                if (input.Version is not null)
                {
                    var current = await ReadAsync(transaction.Connection, ownerId, input.Id!);
                    if (current is null)
                    {
                        await transaction.RollbackAsync();
                        return new SaveResult.NotFound();
                    }

                    if (current.Version != input.Version)
                    {
                        await transaction.RollbackAsync();
                        return new SaveResult.Conflict();
                    }

                    saved = await UpdateAsync(transaction.Connection, ownerId,
                        current with { Title = title, Body = input.Body });
                }
                else
                {
                    saved = await InsertAsync(transaction.Connection, ownerId, title, input.Body);
                }

                await transaction.CommitAsync();
            }
            catch (Exception error)
            {
                await transaction.RollbackAsync();
                throw TranslateError(error);
            }

            PublishChange(principal.Id);
            return new SaveResult.Success(
                new SaveNoteResponse(saved.Id, saved.Title, saved.Body, saved.Version, saved.UpdatedAt));
        }

    }

    internal static class PersistenceLayer
    {
        internal static Task<Note?> ReadAsync(SqliteConnection connection, string ownerId, string id) =>
            connection.QuerySingleOrDefaultAsync<Note>(
                """
                SELECT
                    id AS Id,
                    title AS Title,
                    body AS Body,
                    version AS Version,
                    updated_at AS UpdatedAt
                FROM
                    notes
                WHERE
                    owner_id = @ownerId AND id = @id
                """,
                new { ownerId, id });

        internal static Task<Note> InsertAsync(SqliteConnection connection, string ownerId, string title,
            string body) =>
            connection.QuerySingleAsync<Note>(
                """
                INSERT INTO notes (id, owner_id, title, body, version, updated_at)
                VALUES
                    (@Id, @OwnerId, @Title, @Body, 1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
                RETURNING
                    id AS Id,
                    title AS Title,
                    body AS Body,
                    version AS Version,
                    updated_at AS UpdatedAt
                """,
                new
                {
                    OwnerId = ownerId,
                    Id = Guid.NewGuid().ToString("D"),
                    Title = title,
                    Body = body,
                });

        internal static Task<Note> UpdateAsync(SqliteConnection connection, string ownerId, Note note) =>
            connection.QuerySingleAsync<Note>(
                """
                UPDATE notes
                SET
                    title = @Title,
                    body = @Body,
                    version = version + 1,
                    updated_at = strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
                WHERE
                    owner_id = @OwnerId AND id = @Id
                RETURNING
                    id AS Id,
                    title AS Title,
                    body AS Body,
                    version AS Version,
                    updated_at AS UpdatedAt
                """,
                new
                {
                    OwnerId = ownerId,
                    note.Id,
                    note.Title,
                    note.Body,
                });

        internal static Exception TranslateError(Exception error)
        {
            return error is SqliteException { SqliteExtendedErrorCode: 2067 }
                ? new AppFaultException("TITLE_EXISTS", "同じタイトルのメモが既にあります。")
                : error;
        }
    }

    internal abstract record SaveResult
    {
        internal sealed record Success(SaveNoteResponse Response) : SaveResult;
        internal sealed record NotFound : SaveResult;
        internal sealed record Conflict : SaveResult;
    }
}

public sealed record SaveNoteRequest : IValidatableObject
{
    private string? id;
    private long? version;

    [JsonConstructor]
    public SaveNoteRequest() { }

    internal SaveNoteRequest(string? id, long? version, string title, string body)
    {
        this.id = id;
        this.version = version;
        Title = title;
        Body = body;
    }

    // 省略は新規作成を表す。明示的なnullは入力エラーとする。
    [Uuid]
    [DisallowNull]
    public string? Id { get => id; init => id = value ?? throw new System.Text.Json.JsonException(); }
    [Range(1, long.MaxValue, ErrorMessage = "版は1以上で指定してください。")]
    [DisallowNull]
    public long? Version { get => version; init => version = value ?? throw new System.Text.Json.JsonException(); }
    [JsonRequired]
    [NoteTitle]
    public string Title { get; init; } = null!;
    [JsonRequired]
    [RuneMaxLength(10_000, ErrorMessage = "本文は10,000文字以内で入力してください。")]
    public string Body { get; init; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((Id is null) != (Version is null))
            yield return new ValidationResult("編集対象と版を指定してください。", [nameof(Id), nameof(Version)]);
    }
}

internal sealed record SaveNoteResponse(string Id, string Title, string Body, long Version, string UpdatedAt);

internal sealed class NoteTitleAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string title && !string.IsNullOrWhiteSpace(title) && title.EnumerateRunes().Count() <= 100;

    public override string FormatErrorMessage(string name) => "タイトルは1〜100文字で入力してください。";
}

internal sealed class RuneMaxLengthAttribute(int maximum) : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && text.EnumerateRunes().Count() <= maximum;
}

internal sealed class UuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is null || value is string id && JsonRequest.IsUuid(id);

    public override string FormatErrorMessage(string name) => "IDの形式を確認してください。";
}
