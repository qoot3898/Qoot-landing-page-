using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;
using Qoot.Api.Services;

namespace Qoot.Api.Controllers;

public sealed record LoginRequest(string? Email, string? Password, bool RememberMe);

public sealed record MeDto(Guid Id, string Email, string DisplayName, string Role, string PreferredLanguage);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record ProfileRequest(string? DisplayName, string? PreferredLanguage);

/// <summary>
/// Cookie sign-in for the dashboard. There is no registration endpoint: accounts are created by an
/// administrator or with the create-admin command. Every state-changing call needs the antiforgery
/// token from <c>GET /api/auth/antiforgery</c> in the <c>X-XSRF-TOKEN</c> header.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    SignInManager<AppUser> signIn,
    UserManager<AppUser> users,
    IAntiforgery antiforgery,
    AuditService audit,
    TimeProvider clock) : ControllerBase
{
    public const string LoginAtClaim = "qoot:login_at";

    /// <summary>Issues the antiforgery token (and its HttpOnly cookie). Call again after signing in or out.</summary>
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public IActionResult Antiforgery()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = tokens.RequestToken, headerName = tokens.HeaderName });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<MeDto>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var password = request.Password ?? "";
        var user = email.Length > 0 ? await users.FindByEmailAsync(email) : null;
        if (user is null)
        {
            // Same answer as a wrong password, so the response never reveals which emails have accounts.
            await audit.RecordNowAsync(AuditActions.LoginFailed, "user", "", "Sign-in failed: unknown email", null, email, ct);
            throw ApiError.Unauthorized("login_failed");
        }

        var check = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            var minutes = user.LockoutEnd is { } end ? Math.Max(1, (int)Math.Ceiling((end - clock.GetUtcNow()).TotalMinutes)) : 15;
            await audit.RecordNowAsync(AuditActions.LoginFailed, "user", user.Id.ToString(), "Sign-in refused: account locked", user.Id, email, ct);
            throw ApiError.Unauthorized("locked_out", minutes);
        }

        if (!check.Succeeded)
        {
            await audit.RecordNowAsync(AuditActions.LoginFailed, "user", user.Id.ToString(), "Sign-in failed: wrong password", user.Id, email, ct);
            throw ApiError.Unauthorized("login_failed");
        }

        if (!user.IsActive)
        {
            await audit.RecordNowAsync(AuditActions.LoginFailed, "user", user.Id.ToString(), "Sign-in refused: account turned off", user.Id, email, ct);
            throw ApiError.Unauthorized("account_disabled");
        }

        var loginAt = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        await signIn.SignInWithClaimsAsync(user, new AuthenticationProperties { IsPersistent = request.RememberMe }, [new Claim(LoginAtClaim, loginAt)]);
        user.LastLoginAt = clock.GetUtcNow().UtcDateTime;
        await users.UpdateAsync(user);
        await audit.RecordNowAsync(AuditActions.Login, "user", user.Id.ToString(), "Signed in", user.Id, email, ct);
        return Ok(await MeAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        await signIn.SignOutAsync();
        if (user is not null)
        {
            await audit.RecordNowAsync(AuditActions.Logout, "user", user.Id.ToString(), "Signed out", user.Id, user.Email ?? "", ct);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeDto>> Me()
    {
        var user = await users.GetUserAsync(User) ?? throw ApiError.Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await MeAsync(user));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.GetUserAsync(User) ?? throw ApiError.Unauthorized();
        if (string.IsNullOrEmpty(request.CurrentPassword))
        {
            throw ApiError.Validation("currentPassword", "required");
        }

        if (string.IsNullOrEmpty(request.NewPassword))
        {
            throw ApiError.Validation("newPassword", "required");
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        UserService.ThrowIdentityErrors(result, "newPassword");

        // The security stamp changed, which signs out other sessions; keep this one signed in.
        var loginAt = User.FindFirstValue(LoginAtClaim) ?? clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        await signIn.SignInWithClaimsAsync(user, new AuthenticationProperties { IsPersistent = false }, [new Claim(LoginAtClaim, loginAt)]);
        await audit.RecordNowAsync(AuditActions.PasswordChanged, "user", user.Id.ToString(), "Changed own password", user.Id, user.Email ?? "", ct);
        return NoContent();
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<MeDto>> Profile([FromBody] ProfileRequest request)
    {
        var user = await users.GetUserAsync(User) ?? throw ApiError.Unauthorized();
        if (request.DisplayName is not null)
        {
            var name = SafeText.Clean(request.DisplayName);
            if (name.Length > 120)
            {
                throw ApiError.Validation("displayName", "too_long", 120);
            }

            user.DisplayName = name;
        }

        if (request.PreferredLanguage is not null)
        {
            user.PreferredLanguage = Languages.Normalize(request.PreferredLanguage);
        }

        await users.UpdateAsync(user);
        return Ok(await MeAsync(user));
    }

    private async Task<MeDto> MeAsync(AppUser user)
    {
        var role = (await users.GetRolesAsync(user)).OrderBy(r => r == Roles.Admin ? 0 : 1).FirstOrDefault() ?? Roles.Editor;
        return new MeDto(user.Id, user.Email ?? "", user.DisplayName, role, user.PreferredLanguage);
    }
}
