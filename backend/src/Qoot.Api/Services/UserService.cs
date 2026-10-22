using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Services;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    bool IsLockedOut,
    string PreferredLanguage,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record CreateUserRequest(string? Email, string? DisplayName, string? Role, string? Password);

public sealed record UpdateUserRequest(string? DisplayName, string? Role, bool? IsActive);

public sealed record ResetPasswordRequest(string? NewPassword);

public sealed class UserService(
    QootDbContext db,
    UserManager<AppUser> users,
    AuditService audit,
    ICurrentUser current,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var all = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var roles = await RolesByUserAsync(ct);
        return all.Select(u => ToDto(u, roles.GetValueOrDefault(u.Id, Roles.Editor))).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest r, CancellationToken ct)
    {
        var errors = new FieldErrors();
        var email = (r.Email ?? "").Trim().ToLowerInvariant();
        var name = SafeText.Clean(r.DisplayName);
        var role = r.Role ?? Roles.Editor;
        if (!SafeText.IsEmail(email))
        {
            errors.Add("email", "invalid_email");
        }
        else if (await users.FindByEmailAsync(email) is not null)
        {
            errors.Add("email", "email_taken");
        }

        if (name.Length > 120)
        {
            errors.Add("displayName", "too_long", 120);
        }

        if (!Roles.All.Contains(role))
        {
            errors.Add("role", "invalid_role");
        }

        if (string.IsNullOrEmpty(r.Password))
        {
            errors.Add("password", "required");
        }

        errors.ThrowIfAny();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = name,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            LockoutEnabled = true,
        };
        var result = await users.CreateAsync(user, r.Password!);
        ThrowIdentityErrors(result, "password");
        await users.AddToRoleAsync(user, role);
        audit.Record(AuditActions.UserCreated, "user", user.Id.ToString(), $"Created {email} as {role}");
        await db.SaveChangesAsync(ct);
        return ToDto(user, role);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest r, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw ApiError.NotFound();
        var currentRole = (await users.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Editor;
        var newRole = r.Role ?? currentRole;
        var active = r.IsActive ?? user.IsActive;
        if (!Roles.All.Contains(newRole))
        {
            throw ApiError.Validation("role", "invalid_role");
        }

        var isSelf = current.Id == user.Id;
        if (isSelf && (!active || newRole != Roles.Admin && currentRole == Roles.Admin))
        {
            throw ApiError.BadRequest("cannot_change_self");
        }

        var losesAdmin = currentRole == Roles.Admin && user.IsActive && (newRole != Roles.Admin || !active);
        if (losesAdmin && await ActiveAdminCountAsync(ct) <= 1)
        {
            throw ApiError.BadRequest("last_admin");
        }

        if (r.DisplayName is not null)
        {
            var name = SafeText.Clean(r.DisplayName);
            if (name.Length > 120)
            {
                throw ApiError.Validation("displayName", "too_long", 120);
            }

            user.DisplayName = name;
        }

        var changedAccess = newRole != currentRole || active != user.IsActive;
        user.IsActive = active;
        if (newRole != currentRole)
        {
            await users.RemoveFromRoleAsync(user, currentRole);
            await users.AddToRoleAsync(user, newRole);
        }

        await users.UpdateAsync(user);
        if (changedAccess)
        {
            // Signs the person out everywhere at the next cookie check, so the change applies at once.
            await users.UpdateSecurityStampAsync(user);
        }

        audit.Record(AuditActions.UserUpdated, "user", user.Id.ToString(), $"Updated {user.Email}: role {newRole}, {(active ? "active" : "turned off")}");
        await db.SaveChangesAsync(ct);
        return ToDto(user, newRole);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest r, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw ApiError.NotFound();
        if (string.IsNullOrEmpty(r.NewPassword))
        {
            throw ApiError.Validation("newPassword", "required");
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, r.NewPassword);
        ThrowIdentityErrors(result, "newPassword");
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        audit.Record(AuditActions.UserPasswordReset, "user", user.Id.ToString(), $"Reset the password of {user.Email}");
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlockAsync(Guid id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw ApiError.NotFound();
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        audit.Record(AuditActions.UserUnlocked, "user", user.Id.ToString(), $"Unlocked {user.Email}");
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw ApiError.NotFound();
        if (current.Id == user.Id)
        {
            throw ApiError.BadRequest("cannot_change_self");
        }

        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin) && user.IsActive && await ActiveAdminCountAsync(ct) <= 1)
        {
            throw ApiError.BadRequest("last_admin");
        }

        await users.DeleteAsync(user);
        audit.Record(AuditActions.UserDeleted, "user", user.Id.ToString(), $"Deleted {user.Email}");
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> ActiveAdminCountAsync(CancellationToken ct)
    {
        var adminRoleId = await db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).FirstOrDefaultAsync(ct);
        return await db.UserRoles.Where(ur => ur.RoleId == adminRoleId)
            .Join(db.Users, ur => ur.UserId, u => u.Id, (_, u) => u)
            .CountAsync(u => u.IsActive, ct);
    }

    private async Task<Dictionary<Guid, string>> RolesByUserAsync(CancellationToken ct) =>
        await db.UserRoles.Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .GroupBy(x => x.UserId)
            .Select(g => new { g.Key, Name = g.Min(x => x.Name) })
            .ToDictionaryAsync(x => x.Key, x => x.Name ?? Roles.Editor, ct);

    private UserDto ToDto(AppUser u, string role) => new(
        u.Id, u.Email ?? "", u.DisplayName, role, u.IsActive,
        u.LockoutEnd is { } end && end > clock.GetUtcNow(), u.PreferredLanguage, u.CreatedAt, u.LastLoginAt);

    /// <summary>Turns Identity's errors (password rules, duplicates) into localised field errors.</summary>
    public static void ThrowIdentityErrors(IdentityResult result, string passwordField)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = new FieldErrors();
        foreach (var error in result.Errors)
        {
            switch (error.Code)
            {
                case nameof(IdentityErrorDescriber.PasswordTooShort):
                    errors.Add(passwordField, "password_too_short", PasswordRules.MinLength);
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresDigit):
                    errors.Add(passwordField, "password_requires_digit");
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresLower):
                    errors.Add(passwordField, "password_requires_lower");
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresUpper):
                    errors.Add(passwordField, "password_requires_upper");
                    break;
                case nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars):
                    errors.Add(passwordField, "password_requires_unique");
                    break;
                case nameof(IdentityErrorDescriber.PasswordMismatch):
                    errors.Add("currentPassword", "password_incorrect");
                    break;
                case nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName):
                    errors.Add("email", "email_taken");
                    break;
                case nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName):
                    errors.Add("email", "invalid_email");
                    break;
                default:
                    errors.Add(passwordField, "password_invalid");
                    break;
            }
        }

        errors.ThrowIfAny();
    }
}

public static class PasswordRules
{
    public const int MinLength = 12;
}
