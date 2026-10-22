using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Qoot.Api.Infrastructure;

/// <summary>
/// Every error leaves the API in the same shape (RFC 7807 problem details) with a stable
/// <c>code</c>, a localised <c>title</c> and, for validation, an <c>errors</c> map keyed by field path.
/// Unexpected exceptions are logged with their trace id and never expose internals.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, message, errors, details) = exception switch
        {
            ApiError api => (api.Status, api.Code, api.Message, api.Errors, api.Details),
            AntiforgeryValidationException => (StatusCodes.Status400BadRequest, "antiforgery", Messages.Get("antiforgery"), null, null),
            BadHttpRequestException bad => (bad.StatusCode, "invalid_request", Messages.Get("invalid_request"), null, null),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "cancelled", "", null, null),
            _ => (StatusCodes.Status500InternalServerError, "server_error", Messages.Get("server_error"), null, (object?)null),
        };

        if (status == 499)
        {
            return true;
        }

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled error on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            // An expected outcome (login_failed, locked_out, validation, not_found...): one line with the reason, no stack trace.
            logger.LogInformation("{Method} {Path} answered {StatusCode} {Code}", httpContext.Request.Method, httpContext.Request.Path, status, code);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = message,
            Type = $"https://httpstatuses.io/{status}",
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        if (details is not null)
        {
            problem.Extensions["details"] = details;
        }

        if (status >= 500 && env.IsDevelopment())
        {
            problem.Detail = exception.Message;
        }

        httpContext.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem, Exception = exception });
    }
}

/// <summary>Writes a problem response for statuses produced outside controllers (401/403 from auth, 404, 429).</summary>
public static class ProblemWriter
{
    public static Task WriteAsync(HttpContext context, int status, string code, params object[] args)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(
            new Dictionary<string, object?>
            {
                ["type"] = $"https://httpstatuses.io/{status}",
                ["title"] = Messages.Get(code, args),
                ["status"] = status,
                ["code"] = code,
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier,
            },
            options: null,
            contentType: "application/problem+json");
    }
}
