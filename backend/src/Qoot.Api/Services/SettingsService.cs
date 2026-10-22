using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Content;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Services;

public sealed record SettingsDto(JsonNode Data, Guid ConcurrencyStamp, DateTime UpdatedAt, IReadOnlyList<string> MissingTranslations);

public sealed record SaveSettingsRequest(JsonNode? Data, Guid ConcurrencyStamp);

public sealed class SettingsService(QootDbContext db, ContentVersion version, AuditService audit, ICurrentUser user, TimeProvider clock)
{
    public const int RowId = 1;

    public async Task<SettingsDto> GetAsync(CancellationToken ct)
    {
        var row = await LoadAsync(ct);
        return ToDto(row);
    }

    public async Task<SettingsDto> SaveAsync(SaveSettingsRequest request, CancellationToken ct)
    {
        var row = await LoadAsync(ct);
        SectionService.EnsureStamp(row.ConcurrencyStamp, request.ConcurrencyStamp);

        var ctx = new ContentValidationContext();
        var data = ContentValidator.Validate(SettingsSchema.Schema.Fields, request.Data, ctx);
        if (data["enabledLanguages"] is JsonArray langs && langs.All(l => l?.GetValue<string>() != "ar"))
        {
            ctx.Errors.Add("enabledLanguages", "arabic_required");
        }

        await MediaRefChecker.CheckAsync(db, ctx, ct);
        ctx.Errors.ThrowIfAny();

        row.Json = data.ToJsonString();
        row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        row.UpdatedById = user.Id;
        row.ConcurrencyStamp = Guid.NewGuid();
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.SettingsUpdated, "settings", "1", "Updated site settings");
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    /// <summary>Raw stored settings (including private fields) for server-side use.</summary>
    public async Task<JsonObject> GetRawAsync(CancellationToken ct)
    {
        var json = await db.SiteSettings.AsNoTracking().Where(s => s.Id == RowId).Select(s => s.Json).FirstOrDefaultAsync(ct);
        return JsonDefaults.Parse(json) as JsonObject ?? new JsonObject();
    }

    private async Task<SiteSettings> LoadAsync(CancellationToken ct) =>
        await db.SiteSettings.FirstOrDefaultAsync(s => s.Id == RowId, ct) ?? throw ApiError.NotFound();

    private static SettingsDto ToDto(SiteSettings row)
    {
        var ctx = new ContentValidationContext();
        var data = ContentValidator.Validate(SettingsSchema.Schema.Fields, JsonDefaults.Parse(row.Json), ctx);
        return new SettingsDto(data, row.ConcurrencyStamp, row.UpdatedAt, ctx.MissingTranslations);
    }
}
