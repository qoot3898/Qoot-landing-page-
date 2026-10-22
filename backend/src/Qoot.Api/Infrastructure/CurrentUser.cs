using System.Security.Claims;
using Qoot.Api.Domain;

namespace Qoot.Api.Infrastructure;

public interface ICurrentUser
{
    Guid? Id { get; }
    string Email { get; }
    bool IsAdmin { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.Identity?.Name ?? "";

    public bool IsAdmin => Principal?.IsInRole(Roles.Admin) == true;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var ua = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(ua) ? null : ua.Length > 300 ? ua[..300] : ua;
        }
    }
}
