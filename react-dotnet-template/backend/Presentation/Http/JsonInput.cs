using Aidd.ReactDotnet.Domain;
using System.Text.Json;
using Aidd.ReactDotnet.Features.Notes;

namespace Aidd.ReactDotnet.Presentation.Http;

internal static class JsonInput
{
    internal static async Task<(string Id, long Version)> ReadRemove(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await JsonRequest.ReadObject(request, new[] { "id", "version" }, cancellationToken);
        var root = document.RootElement;
        var id = JsonRequest.RequiredString(root, "id");
        if (!JsonRequest.IsUuid(id))
        {
            throw AppFaultException.Validation();
        }

        return (id, JsonRequest.PositiveInteger(JsonRequest.Required(root, "version")));
    }

    internal static async Task<BulkInput> ReadBulk(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await JsonRequest.ReadObject(request, new[] { "titles", "body" }, cancellationToken);
        return new BulkInput(
            JsonRequest.RequiredString(document.RootElement, "titles"),
            JsonRequest.RequiredString(document.RootElement, "body"));
    }

    internal static async Task<string> ReadDemoUser(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await JsonRequest.ReadObject(request, new[] { "user" }, cancellationToken);
        return JsonRequest.RequiredString(document.RootElement, "user");
    }

}
