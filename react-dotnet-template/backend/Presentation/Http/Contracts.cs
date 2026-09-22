using System.Text.Json.Serialization;

namespace Aidd.ReactDotnet.Presentation.Http;

internal sealed record PublicAppError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? FieldErrors = null);

internal sealed record AppErrorEnvelope(PublicAppError AppError);
