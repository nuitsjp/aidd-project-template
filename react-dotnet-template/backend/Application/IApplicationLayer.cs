using Aidd.ReactDotnet.Application.Authentication;
using System.Diagnostics.CodeAnalysis;

namespace Aidd.ReactDotnet.Application;

internal delegate bool TryValidateDelegate<TRequest>(
    TRequest input,
    [NotNullWhen(false)] out IReadOnlyDictionary<string, string[]>? errors);

internal interface IApplicationLayer<TRequest, TResult>
{
    bool TryValidate(
        TRequest input,
        [NotNullWhen(false)] out IReadOnlyDictionary<string, string[]>? errors);

    Task<TResult> ExecuteAsync(Principal principal, TRequest input);
}
