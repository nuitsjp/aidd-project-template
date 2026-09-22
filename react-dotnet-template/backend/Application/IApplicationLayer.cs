using Aidd.ReactDotnet.Application.Authentication;

namespace Aidd.ReactDotnet.Application;

internal interface IApplicationLayer<TRequest, TResult>
{
    Task<TResult> ExecuteAsync(Principal principal, TRequest input);
}
