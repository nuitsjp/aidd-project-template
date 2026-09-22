using Aidd.ReactDotnet.Domain;
using System.Text.Json;

namespace Aidd.ReactDotnet.Presentation.Http;

internal static class JsonRequest
{
    internal static bool IsUuid(string value) =>
        value.Length == 36 &&
        value[8] == '-' && value[13] == '-' && value[18] == '-' && value[23] == '-' &&
        Guid.TryParseExact(value, "D", out _);

    internal static async Task<JsonDocument> ReadObject(
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

    internal static JsonElement Required(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            throw AppFaultException.Validation();
        }

        return element;
    }

    internal static string RequiredString(JsonElement root, string name) => String(Required(root, name));

    internal static string String(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw AppFaultException.Validation();
        }

        return element.GetString()!;
    }

    internal static long PositiveInteger(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var value) || value <= 0)
        {
            throw AppFaultException.Validation();
        }

        return value;
    }
}
