using System.Text.Json.Serialization;

namespace Aidd.ReactDotnet.Features.Notes;

internal sealed record BulkInput(
    [property: JsonRequired] string Titles,
    [property: JsonRequired] string Body);
