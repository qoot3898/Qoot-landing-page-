using Microsoft.EntityFrameworkCore;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Services;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public static class Paging
{
    public static (int Page, int Size) Normalize(int? page, int? size, int defaultSize = 20, int maxSize = 100)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var s = size is null or < 1 ? defaultSize : Math.Min(size.Value, maxSize);
        return (p, s);
    }

    public static async Task<PagedResult<T>> ToPageAsync<T>(this IQueryable<T> query, int page, int size, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<T>(items, page, size, total);
    }
}

/// <summary>
/// Tracks a version number for everything visitors can see. Each public-facing change bumps it in the
/// same transaction, and the public endpoints use it as their cache key and ETag.
/// </summary>
public sealed class ContentVersion(QootDbContext db, TimeProvider clock)
{
    public const int RowId = 1;

    /// <summary>Marks public content as changed; saved together with the caller's changes.</summary>
    public async Task MarkChangedAsync(CancellationToken ct)
    {
        var state = await db.ContentStates.FirstOrDefaultAsync(s => s.Id == RowId, ct);
        if (state is null)
        {
            state = new ContentState { Id = RowId };
            db.ContentStates.Add(state);
        }

        state.Version++;
        state.ChangedAt = clock.GetUtcNow().UtcDateTime;
    }

    public async Task<long> GetAsync(CancellationToken ct) =>
        await db.ContentStates.AsNoTracking().Where(s => s.Id == RowId).Select(s => s.Version).FirstOrDefaultAsync(ct);
}

public sealed class AuditService(QootDbContext db, ICurrentUser user, TimeProvider clock)
{
    /// <summary>Adds an audit entry to the current unit of work (saved with the change it describes).</summary>
    public void Record(string action, string entityType, string entityId, string summary)
    {
        db.AuditLog.Add(new AuditEntry
        {
            At = clock.GetUtcNow().UtcDateTime,
            UserId = user.Id,
            UserEmail = user.Email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary.Length > 500 ? summary[..500] : summary,
            IpAddress = user.IpAddress,
        });
    }

    /// <summary>Writes an entry immediately, for events with no other change to save (sign-in attempts).</summary>
    public async Task RecordNowAsync(string action, string entityType, string entityId, string summary, Guid? userId, string email, CancellationToken ct)
    {
        db.AuditLog.Add(new AuditEntry
        {
            At = clock.GetUtcNow().UtcDateTime,
            UserId = userId,
            UserEmail = email.Length > 256 ? email[..256] : email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            IpAddress = user.IpAddress,
        });
        await db.SaveChangesAsync(ct);
    }
}

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string PasswordChanged = "auth.password_changed";
    public const string SectionSaved = "section.draft_saved";
    public const string SectionPublished = "section.published";
    public const string SectionDiscarded = "section.draft_discarded";
    public const string SectionVisibility = "section.visibility";
    public const string SectionsReordered = "section.reordered";
    public const string SettingsUpdated = "settings.updated";
    public const string NewsCreated = "news.created";
    public const string NewsUpdated = "news.updated";
    public const string NewsPublished = "news.published";
    public const string NewsUnpublished = "news.unpublished";
    public const string NewsDeleted = "news.deleted";
    public const string CategoryChanged = "news_category.changed";
    public const string CategoryDeleted = "news_category.deleted";
    public const string MediaUploaded = "media.uploaded";
    public const string MediaReplaced = "media.replaced";
    public const string MediaUpdated = "media.updated";
    public const string MediaDeleted = "media.deleted";
    public const string MessageUpdated = "message.updated";
    public const string MessageDeleted = "message.deleted";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserDeleted = "user.deleted";
    public const string UserUnlocked = "user.unlocked";
}
