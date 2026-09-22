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
using System.Text.Json;
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

    internal void Map(WebApplication app, IdentityService identity) => Presentation.Map(app, identity);

    internal sealed class PresentationLayer
    {
        internal PresentationLayer(IApplicationLayer<SaveNoteRequest, SaveResult> application)
        {
            TryValidate = application.TryValidate;
            ExecuteAsync = application.ExecuteAsync;
        }

        internal TryValidateDelegate<SaveNoteRequest> TryValidate { get; set; }
        internal Func<Principal, SaveNoteRequest, Task<SaveResult>> ExecuteAsync { get; set; }

        internal void Map(WebApplication app, IdentityService identity)
        {
            app.MapAuthenticatedPost<SaveNoteRequest>(
                "/api/notes/save",
                nameof(SaveNote),
                identity,
                HandleAsync)
            .Produces<SaveNoteResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
        }

        internal async Task<IResult> HandleAsync(Principal principal, SaveNoteRequest request)
        {
            if (!TryValidate(request, out var errors))
            {
                return TypedResults.ValidationProblem(
                    errors.ToDictionary(item => item.Key, item => item.Value),
                    title: "入力内容を確認してください。");
            }

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
        internal ApplicationLayer(
            Database database,
            ChangeNotifications notifications)
        {
            BeginTransaction = () => database.BeginTransactionAsync();
            Read = PersistenceLayer.Read;
            Insert = PersistenceLayer.Insert;
            Update = PersistenceLayer.Update;
            TranslateError = PersistenceLayer.TranslateError;
            NewId = Guid.NewGuid;
            UtcNow = () => DateTimeOffset.UtcNow;
            PublishChange = notifications.Publish;
        }

        internal Func<Task<ITransaction>> BeginTransaction { get; set; }
        internal Func<SqliteConnection, string, string, Task<Note?>> Read { get; set; }
        internal Func<SqliteConnection, PreparedSave, Task<int>> Insert { get; set; }
        internal Func<SqliteConnection, PreparedSave, Task<int>> Update { get; set; }
        internal Func<Exception, Exception> TranslateError { get; set; }
        internal Func<Guid> NewId { get; set; }
        internal Func<DateTimeOffset> UtcNow { get; set; }
        internal Func<string, bool> PublishChange { get; set; }

        public bool TryValidate(
            SaveNoteRequest input,
            [NotNullWhen(false)] out IReadOnlyDictionary<string, string[]>? errors)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
            var found = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                var members = result.MemberNames.Take(2).ToArray();
                var key = members.Length == 1
                    ? JsonNamingPolicy.CamelCase.ConvertName(members[0])
                    : string.Empty;
                if (!found.TryGetValue(key, out var messages))
                {
                    messages = [];
                    found[key] = messages;
                }

                messages.Add(result.ErrorMessage ?? "入力内容を確認してください。");
            }

            errors = found.Count == 0
                ? null
                : found.ToDictionary(item => item.Key, item => item.Value.ToArray());
            return errors is null;
        }

        public async Task<SaveResult> ExecuteAsync(Principal principal, SaveNoteRequest input)
        {
            var title = NoteRules.NormalizeTitle(input.Title);
            var prepared = new PreparedSave(principal.Id, input.Id ?? NewId().ToString("D"), input.Version,
                title, input.Body, UtcNow());
            await using var transaction = await BeginTransaction();
            Note saved;
            try
            {
                if (prepared.Version is not null)
                {
                    var current = await Read(transaction.Connection, prepared.OwnerId, prepared.Id);
                    if (current is null)
                    {
                        await transaction.RollbackAsync();
                        return new SaveResult.NotFound();
                    }

                    if (current.Version != prepared.Version)
                    {
                        await transaction.RollbackAsync();
                        return new SaveResult.Conflict();
                    }

                    await Update(transaction.Connection, prepared);
                }
                else
                {
                    await Insert(transaction.Connection, prepared);
                }

                saved = await Read(transaction.Connection, prepared.OwnerId, prepared.Id)
                    ?? throw new InvalidOperationException("保存結果を取得できませんでした。");
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
        internal static Task<Note?> Read(SqliteConnection connection, string ownerId, string id) =>
            connection.QuerySingleOrDefaultAsync<Note>(
                "SELECT id AS Id,title AS Title,body AS Body,version AS Version,updated_at AS UpdatedAt FROM notes WHERE owner_id=@ownerId AND id=@id",
                new { ownerId, id });

        internal static Task<int> Insert(SqliteConnection connection, PreparedSave input) =>
            connection.ExecuteAsync(
                "INSERT INTO notes(id,owner_id,title,body,version,updated_at) VALUES(@Id,@OwnerId,@Title,@Body,1,@UpdatedAt)",
                new
                {
                    OwnerId = input.OwnerId,
                    Id = input.Id,
                    Title = input.Title,
                    Body = input.Body,
                    UpdatedAt = input.UpdatedAt.ToUniversalTime().ToString("O"),
                });

        internal static Task<int> Update(SqliteConnection connection, PreparedSave input) =>
            connection.ExecuteAsync(
                "UPDATE notes SET title=@Title,body=@Body,version=version+1,updated_at=@UpdatedAt WHERE owner_id=@OwnerId AND id=@Id",
                new
                {
                    OwnerId = input.OwnerId,
                    Id = input.Id,
                    Title = input.Title,
                    Body = input.Body,
                    UpdatedAt = input.UpdatedAt.ToUniversalTime().ToString("O"),
                });

        internal static Exception TranslateError(Exception error)
        {
            return error is SqliteException { SqliteExtendedErrorCode: 2067 }
                ? new AppFaultException("TITLE_EXISTS", "同じタイトルのメモが既にあります。")
                : error;
        }
    }

    internal sealed record PreparedSave(string OwnerId, string Id, long? Version, string Title, string Body, DateTimeOffset UpdatedAt);

    internal abstract record SaveResult
    {
        internal sealed record Success(SaveNoteResponse Response) : SaveResult;
        internal sealed record NotFound : SaveResult;
        internal sealed record Conflict : SaveResult;
    }
}

internal sealed record SaveNoteRequest : IValidatableObject
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
    public override bool IsValid(object? value) => value is string title && NoteRules.IsValidTitle(title);

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
