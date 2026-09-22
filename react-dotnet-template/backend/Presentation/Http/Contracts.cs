using System.Text.Json.Serialization;

namespace Aidd.ReactDotnet.Presentation.Http;

internal sealed record SessionOutput(Aidd.ReactDotnet.Application.Authentication.Principal? User, string Mode);
internal sealed record SignInOutput(Aidd.ReactDotnet.Application.Authentication.Principal User);
internal sealed record SuccessOutput(bool Ok);
internal sealed record DemoSignInInput([property: JsonRequired] string User);
internal sealed record RemoveNoteInput([property: JsonRequired] string Id, [property: JsonRequired] long Version);

internal sealed record HealthOutput(string Status);
