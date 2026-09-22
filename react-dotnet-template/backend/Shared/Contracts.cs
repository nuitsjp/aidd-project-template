using System.Text.Json.Serialization;

namespace Aidd.ReactDotnet.Shared;

internal sealed record Principal(string Id, string Name);

internal sealed record PublicAppError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? FieldErrors = null);

internal sealed record AppErrorEnvelope(PublicAppError AppError);
