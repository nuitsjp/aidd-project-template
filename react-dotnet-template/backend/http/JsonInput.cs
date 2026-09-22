using System.Text.Json;
using Aidd.ReactDotnet.Features.Notes;
using Aidd.ReactDotnet.Shared;

namespace Aidd.ReactDotnet.Http;

internal static class JsonInput
{
    internal static async Task<SaveNote> ReadSaveNote(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ReadObject(request, new[] { "id", "version", "title", "body" }, cancellationToken);
        var root = document.RootElement;
        var title = RequiredString(root, "title");
        var body = RequiredString(root, "body");
        string? id = null;
        long? version = null;
        if (root.TryGetProperty("id", out var idElement))
        {
            id = String(idElement);
            if (!IsUuid(id))
            {
                throw AppFaultException.Validation();
            }
        }

        if (root.TryGetProperty("version", out var versionElement))
        {
            version = PositiveInteger(versionElement);
        }

        return new SaveNote(id, version, title, body);
    }

    internal static async Task<(string Id, long Version)> ReadRemove(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ReadObject(request, new[] { "id", "version" }, cancellationToken);
        var root = document.RootElement;
        var id = RequiredString(root, "id");
        if (!IsUuid(id))
        {
            throw AppFaultException.Validation();
        }

        return (id, PositiveInteger(Required(root, "version")));
    }

    internal static async Task<BulkInput> ReadBulk(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ReadObject(request, new[] { "titles", "body" }, cancellationToken);
        return new BulkInput(
            RequiredString(document.RootElement, "titles"),
            RequiredString(document.RootElement, "body"));
    }

    internal static async Task<string> ReadDemoUser(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ReadObject(request, new[] { "user" }, cancellationToken);
        return RequiredString(document.RootElement, "user");
    }

    internal static bool IsUuid(string value) =>
        value.Length == 36 &&
        value[8] == '-' && value[13] == '-' && value[18] == '-' && value[23] == '-' &&
        Guid.TryParseExact(value, "D", out _);

    private static async Task<JsonDocument> ReadObject(
        HttpRequest request,
        IReadOnlyCollection<string> allowedProperties,
        CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw AppFaultException.Validation();
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw AppFaultException.Validation();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name) || !seen.Add(property.Name))
            {
                document.Dispose();
                throw AppFaultException.Validation();
            }
        }

        return document;
    }

    private static JsonElement Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            throw AppFaultException.Validation();
        }

        return element;
    }

    private static string RequiredString(JsonElement root, string name) => String(Required(root, name));

    private static string String(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw AppFaultException.Validation();
        }

        return element.GetString()!;
    }

    private static long PositiveInteger(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var value) || value <= 0)
        {
            throw AppFaultException.Validation();
        }

        return value;
    }
}
