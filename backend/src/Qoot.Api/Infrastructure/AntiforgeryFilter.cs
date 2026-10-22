using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Qoot.Api.Infrastructure;

/// <summary>
/// Requires a valid antiforgery token (X-XSRF-TOKEN header, tied to the HttpOnly antiforgery cookie and
/// the signed-in user) on every POST, PUT, PATCH and DELETE, except actions marked
/// [IgnoreAntiforgeryToken] (the anonymous Contact us form, which carries no session).
/// </summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        var policy = context.ActionDescriptor.EndpointMetadata.LastOrDefault(m => m is IgnoreAntiforgeryTokenAttribute or IAntiforgeryMetadata);
        if (policy is IgnoreAntiforgeryTokenAttribute or IAntiforgeryMetadata { RequiresValidation: false })
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            context.Result = new ObjectResult(new Dictionary<string, object?>
            {
                ["type"] = "https://httpstatuses.io/400",
                ["title"] = Messages.Get("antiforgery"),
                ["status"] = 400,
                ["code"] = "antiforgery",
            })
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" },
            };
        }
    }
}
