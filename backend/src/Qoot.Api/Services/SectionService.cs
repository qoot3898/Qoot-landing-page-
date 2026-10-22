using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Content;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Services;

public sealed record SectionSummaryDto(
    string Key,
    Bilingual Name,
    Bilingual Description,
    string? Anchor,
    int SortOrder,
    bool IsVisible,
    bool Pinned,
    bool CanHide,
    bool IsPublished,
    bool HasUnpublishedChanges,
    DateTime DraftUpdatedAt,
    DateTime? PublishedAt,
    int MissingTranslations);

public sealed record SectionDetailDto(
    SectionSummaryDto Section,
    JsonNode Draft,
    JsonNode? Published,
    Guid ConcurrencyStamp,
    IReadOnlyList<string> MissingTranslations,
    string? DraftUpdatedBy,
    string? PublishedBy);

public sealed record SaveSectionRequest(JsonNode? Data, Guid ConcurrencyStamp);

public sealed record VisibilityRequest(bool Visible);

public sealed record ReorderRequest(IReadOnlyList<string> Keys);

public sealed class SectionService(QootDbContext db, ContentVersion version, AuditService audit, ICurrentUser user, TimeProvider clock)
{
    public async Task<IReadOnlyList<SectionSummaryDto>> ListAsync(CancellationToken ct)
    {
        var sections = await db.Sections.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        return sections.Where(s => SectionSchemas.Find(s.Key) is not null).Select(Summarize).ToList();
    }

    public async Task<SectionDetailDto> GetAsync(string key, CancellationToken ct)
    {
        var section = await FindAsync(key, ct);
        return await DetailAsync(section, ct);
    }

    public async Task<SectionDetailDto> SaveDraftAsync(string key, SaveSectionRequest request, CancellationToken ct)
    {
        var section = await FindAsync(key, ct);
        var schema = SectionSchemas.Find(key)!;
        EnsureStamp(section.ConcurrencyStamp, request.ConcurrencyStamp);

        var ctx = new ContentValidationContext();
        var data = ContentValidator.Validate(schema.Fields, request.Data, ctx);
        await MediaRefChecker.CheckAsync(db, ctx, ct);
        ctx.Errors.ThrowIfAny();

        section.DraftJson = data.ToJsonString();
        section.HasUnpublishedChanges = !JsonEquals(section.DraftJson, section.PublishedJson);
        section.DraftUpdatedAt = clock.GetUtcNow().UtcDateTime;
        section.DraftUpdatedById = user.Id;
        section.ConcurrencyStamp = Guid.NewGuid();
        audit.Record(AuditActions.SectionSaved, "section", key, $"Saved the draft of {schema.Name.En}");
        await db.SaveChangesAsync(ct);
        return await DetailAsync(section, ct);
    }

    public async Task<SectionDetailDto> PublishAsync(string key, CancellationToken ct)
    {
        var section = await FindAsync(key, ct);
        var schema = SectionSchemas.Find(key)!;

        // Re-validate what is about to go live (media may have been deleted since the draft was saved).
        var ctx = new ContentValidationContext();
        var data = ContentValidator.Validate(schema.Fields, JsonDefaults.Parse(section.DraftJson), ctx);
        await MediaRefChecker.CheckAsync(db, ctx, ct);
        ctx.Errors.ThrowIfAny();

        section.DraftJson = data.ToJsonString();
        section.PublishedJson = section.DraftJson;
        section.HasUnpublishedChanges = false;
        section.PublishedAt = clock.GetUtcNow().UtcDateTime;
        section.PublishedById = user.Id;
        section.ConcurrencyStamp = Guid.NewGuid();
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.SectionPublished, "section", key, $"Published {schema.Name.En}");
        await db.SaveChangesAsync(ct);
        return await DetailAsync(section, ct);
    }

    public async Task<IReadOnlyList<SectionSummaryDto>> PublishAllAsync(CancellationToken ct)
    {
        var pending = await db.Sections.Where(s => s.HasUnpublishedChanges).Select(s => s.Key).ToListAsync(ct);
        foreach (var key in pending)
        {
            await PublishAsync(key, ct);
        }

        return await ListAsync(ct);
    }

    public async Task<SectionDetailDto> DiscardAsync(string key, CancellationToken ct)
    {
        var section = await FindAsync(key, ct);
        if (section.PublishedJson is null || !section.HasUnpublishedChanges)
        {
            throw ApiError.BadRequest("nothing_to_publish");
        }

        section.DraftJson = section.PublishedJson;
        section.HasUnpublishedChanges = false;
        section.DraftUpdatedAt = clock.GetUtcNow().UtcDateTime;
        section.DraftUpdatedById = user.Id;
        section.ConcurrencyStamp = Guid.NewGuid();
        audit.Record(AuditActions.SectionDiscarded, "section", key, $"Discarded unpublished changes to {SectionSchemas.Find(key)!.Name.En}");
        await db.SaveChangesAsync(ct);
        return await DetailAsync(section, ct);
    }

    public async Task<SectionSummaryDto> SetVisibilityAsync(string key, bool visible, CancellationToken ct)
    {
        var section = await FindAsync(key, ct);
        var schema = SectionSchemas.Find(key)!;
        if (!schema.CanHide && !visible)
        {
            throw ApiError.BadRequest("section_cannot_hide");
        }

        if (section.IsVisible != visible)
        {
            section.IsVisible = visible;
            await version.MarkChangedAsync(ct);
            audit.Record(AuditActions.SectionVisibility, "section", key, $"{(visible ? "Showed" : "Hid")} {schema.Name.En}");
            await db.SaveChangesAsync(ct);
        }

        return Summarize(section);
    }

    /// <summary>Reorders the sections between the header and the footer.</summary>
    public async Task<IReadOnlyList<SectionSummaryDto>> ReorderAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var sections = await db.Sections.ToListAsync(ct);
        var movable = sections.Where(s => SectionSchemas.Find(s.Key) is { Pinned: false }).ToList();
        if (keys.Count != movable.Count || keys.Distinct(StringComparer.Ordinal).Count() != keys.Count || keys.Any(k => movable.All(s => s.Key != k)))
        {
            throw ApiError.BadRequest("invalid_order");
        }

        var header = sections.FirstOrDefault(s => s.Key == SectionSchemas.Header);
        var footer = sections.FirstOrDefault(s => s.Key == SectionSchemas.Footer);
        if (header is not null)
        {
            header.SortOrder = 0;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            movable.First(s => s.Key == keys[i]).SortOrder = (i + 1) * 10;
        }

        if (footer is not null)
        {
            footer.SortOrder = 10_000;
        }

        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.SectionsReordered, "section", "*", "Reordered sections: " + string.Join(", ", keys));
        await db.SaveChangesAsync(ct);
        return await ListAsync(ct);
    }

    private async Task<Section> FindAsync(string key, CancellationToken ct)
    {
        if (SectionSchemas.Find(key) is null)
        {
            throw ApiError.NotFound("unknown_section");
        }

        return await db.Sections.FirstOrDefaultAsync(s => s.Key == key, ct) ?? throw ApiError.NotFound("unknown_section");
    }

    private async Task<SectionDetailDto> DetailAsync(Section section, CancellationToken ct)
    {
        var ids = new[] { section.DraftUpdatedById, section.PublishedById }.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName.Length > 0 ? u.DisplayName : u.Email ?? "", ct);
        return new SectionDetailDto(
            Summarize(section),
            JsonDefaults.Parse(section.DraftJson) ?? new JsonObject(),
            JsonDefaults.Parse(section.PublishedJson),
            section.ConcurrencyStamp,
            MissingTranslations(section),
            section.DraftUpdatedById is { } d ? names.GetValueOrDefault(d) : null,
            section.PublishedById is { } p ? names.GetValueOrDefault(p) : null);
    }

    private static SectionSummaryDto Summarize(Section s)
    {
        var schema = SectionSchemas.Find(s.Key)!;
        return new SectionSummaryDto(
            s.Key, schema.Name, schema.Description, schema.Anchor, s.SortOrder, s.IsVisible, schema.Pinned, schema.CanHide,
            s.PublishedJson is not null, s.HasUnpublishedChanges, s.DraftUpdatedAt, s.PublishedAt, MissingTranslations(s).Count);
    }

    private static List<string> MissingTranslations(Section s)
    {
        var ctx = new ContentValidationContext();
        ContentValidator.Validate(SectionSchemas.Find(s.Key)!.Fields, JsonDefaults.Parse(s.DraftJson), ctx);
        return ctx.MissingTranslations;
    }

    internal static void EnsureStamp(Guid current, Guid supplied)
    {
        if (current != supplied)
        {
            throw ApiError.Conflict("concurrency_conflict");
        }
    }

    private static bool JsonEquals(string a, string? b) =>
        b is not null && JsonNode.DeepEquals(JsonDefaults.Parse(a), JsonDefaults.Parse(b));
}

/// <summary>Confirms that every picture referenced by content still exists and is the right kind.</summary>
public static class MediaRefChecker
{
    public static async Task CheckAsync(QootDbContext db, ContentValidationContext ctx, CancellationToken ct)
    {
        if (ctx.MediaRefs.Count == 0)
        {
            return;
        }

        var ids = ctx.MediaRefs.Select(r => r.Id).Distinct().ToList();
        var found = await db.Media.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.ContentType, ct);
        foreach (var reference in ctx.MediaRefs)
        {
            if (!found.TryGetValue(reference.Id, out var type))
            {
                ctx.Errors.Add(reference.Path, "media_not_found");
            }
            else if (!reference.AllowDrawings && type == "image/svg+xml")
            {
                ctx.Errors.Add(reference.Path, "media_not_photo");
            }
        }
    }
}
