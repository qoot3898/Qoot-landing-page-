using Microsoft.EntityFrameworkCore;
using Qoot.Api.Content;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Services;

public sealed record DashboardDto(
    SectionStats Sections,
    NewsStats News,
    MessageStats Messages,
    MediaStats Media,
    IReadOnlyList<DailyCount> RequestsPerDay,
    IReadOnlyList<SubmissionDto> RecentMessages,
    IReadOnlyList<AuditDto>? RecentActivity,
    DateTime? LastPublishedAt);

public sealed record SectionStats(int Total, int Visible, int Hidden, int WithUnpublishedChanges, int MissingTranslations);

public sealed record NewsStats(int Total, int Published, int Scheduled, int Drafts, int Featured);

public sealed record MessageStats(int Total, int New, int Unread, int Last7Days, int Last30Days);

public sealed record MediaStats(int Total, int Uploads, long UploadBytes);

public sealed record DailyCount(DateOnly Day, int Count);

public sealed record AuditDto(long Id, DateTime At, string UserEmail, string Action, string EntityType, string EntityId, string Summary, string? IpAddress);

public sealed record AuditQuery(string? Search, string? Action, int? Page, int? PageSize);

public sealed class DashboardService(QootDbContext db, SectionService sections, ContactService contacts, ICurrentUser user, TimeProvider clock)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sectionList = await sections.ListAsync(ct);

        var news = await db.News.AsNoTracking().Select(n => new { n.Status, n.PublishedAt, n.IsFeatured }).ToListAsync(ct);
        var newsStats = new NewsStats(
            news.Count,
            news.Count(n => n.Status == NewsStatus.Published && n.PublishedAt <= now),
            news.Count(n => n.Status == NewsStatus.Published && n.PublishedAt > now),
            news.Count(n => n.Status == NewsStatus.Draft),
            news.Count(n => n.IsFeatured));

        var since30 = now.Date.AddDays(-29);
        var since14 = now.Date.AddDays(-13);
        var messageCounts = new MessageStats(
            await db.ContactSubmissions.CountAsync(ct),
            await db.ContactSubmissions.CountAsync(s => s.Status == SubmissionStatus.New, ct),
            await db.ContactSubmissions.CountAsync(s => !s.IsRead, ct),
            await db.ContactSubmissions.CountAsync(s => s.CreatedAt >= now.AddDays(-7), ct),
            await db.ContactSubmissions.CountAsync(s => s.CreatedAt >= since30, ct));

        var recentDates = await db.ContactSubmissions.AsNoTracking().Where(s => s.CreatedAt >= since14).Select(s => s.CreatedAt).ToListAsync(ct);
        var perDay = Enumerable.Range(0, 14)
            .Select(i => DateOnly.FromDateTime(since14.AddDays(i)))
            .Select(day => new DailyCount(day, recentDates.Count(d => DateOnly.FromDateTime(d) == day)))
            .ToList();

        var media = new MediaStats(
            await db.Media.CountAsync(ct),
            await db.Media.CountAsync(m => m.Kind == MediaKind.Upload, ct),
            await db.Media.Where(m => m.Kind == MediaKind.Upload).SumAsync(m => (long?)m.SizeBytes, ct) ?? 0);

        var recent = await contacts.ListAsync(new SubmissionQuery("open", null, 1, 5), ct);
        IReadOnlyList<AuditDto>? activity = null;
        if (user.IsAdmin)
        {
            activity = await db.AuditLog.AsNoTracking().OrderByDescending(a => a.Id).Take(8)
                .Select(a => new AuditDto(a.Id, a.At, a.UserEmail, a.Action, a.EntityType, a.EntityId, a.Summary, a.IpAddress))
                .ToListAsync(ct);
        }

        var lastPublished = await db.ContentStates.AsNoTracking().Select(s => (DateTime?)s.ChangedAt).FirstOrDefaultAsync(ct);

        return new DashboardDto(
            new SectionStats(
                sectionList.Count,
                sectionList.Count(s => s.IsVisible),
                sectionList.Count(s => !s.IsVisible),
                sectionList.Count(s => s.HasUnpublishedChanges),
                sectionList.Sum(s => s.MissingTranslations)),
            newsStats,
            messageCounts,
            media,
            perDay,
            recent.Items,
            activity,
            lastPublished);
    }

    public async Task<PagedResult<AuditDto>> AuditAsync(AuditQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(q.Page, q.PageSize, defaultSize: 50, maxSize: 200);
        var query = db.AuditLog.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Action))
        {
            var prefix = q.Action.Trim() + "%";
            query = query.Where(a => EF.Functions.Like(a.Action, prefix));
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(a => EF.Functions.Like(a.Summary, term) || EF.Functions.Like(a.UserEmail, term) || EF.Functions.Like(a.EntityId, term));
        }

        var result = await query.OrderByDescending(a => a.Id)
            .Select(a => new AuditDto(a.Id, a.At, a.UserEmail, a.Action, a.EntityType, a.EntityId, a.Summary, a.IpAddress))
            .ToPageAsync(page, size, ct);
        return result;
    }
}
