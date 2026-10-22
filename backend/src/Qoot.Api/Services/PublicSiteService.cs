using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Qoot.Api.Content;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Media;

namespace Qoot.Api.Services;

public static class Languages
{
    public static readonly string[] Supported = ["ar", "en"];

    public static string Normalize(string? lang) => lang?.Trim().ToLowerInvariant() is "en" ? "en" : "ar";

    public static string Dir(string lang) => lang == "ar" ? "rtl" : "ltr";

    public static string Label(string lang) => lang == "ar" ? "العربية" : "English";

    public static string HomePath(string lang) => lang == "ar" ? "/" : "/en/";

    public static string NewsPath(string lang) => lang == "ar" ? "/news/" : "/en/news/";

    public static string ArticlePath(string lang, string slug) => NewsPath(lang) + Uri.EscapeDataString(slug);
}

/// <summary>Everything the public page needs in one response: settings, then each visible section in order.</summary>
public sealed class PublicSiteService(
    QootDbContext db,
    ContentVersion version,
    IMemoryCache cache,
    MediaUrls urls,
    SeoBuilder seo,
    TimeProvider clock)
{
    public sealed record SitePayload(JsonObject Json, long Version);

    public async Task<SitePayload> GetAsync(string lang, bool preview, CancellationToken ct)
    {
        var current = await version.GetAsync(ct);
        if (preview)
        {
            return new SitePayload(await BuildAsync(lang, preview: true, current, ct), current);
        }

        var key = $"site:{lang}:{current}";
        if (cache.TryGetValue(key, out JsonObject? cached) && cached is not null)
        {
            return new SitePayload((JsonObject)cached.DeepClone(), current);
        }

        var built = await BuildAsync(lang, preview: false, current, ct);
        cache.Set(key, built, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10), Size = 1 });
        return new SitePayload((JsonObject)built.DeepClone(), current);
    }

    private async Task<JsonObject> BuildAsync(string lang, bool preview, long current, CancellationToken ct)
    {
        var sections = await db.Sections.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(ct);
        var settingsJson = JsonDefaults.Parse(await db.SiteSettings.AsNoTracking().Select(s => s.Json).FirstOrDefaultAsync(ct));

        var chosen = new List<(Section Section, SectionSchema Schema, JsonNode? Data)>();
        foreach (var section in sections)
        {
            if (SectionSchemas.Find(section.Key) is not { } schema)
            {
                continue;
            }

            var json = preview ? section.DraftJson : section.PublishedJson;
            if (json is null || (!section.IsVisible && schema.CanHide))
            {
                continue;
            }

            chosen.Add((section, schema, JsonDefaults.Parse(json)));
        }

        var mediaIds = new HashSet<int>();
        MediaRefCollector.Collect(SettingsSchema.Schema.Fields, settingsJson, mediaIds);
        foreach (var (_, schema, data) in chosen)
        {
            MediaRefCollector.Collect(schema.Fields, data, mediaIds);
        }

        var media = await db.Media.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var localizer = new ContentLocalizer(lang, media, urls);
        var settings = localizer.Localize(SettingsSchema.Schema.Fields, settingsJson);

        var enabled = (settingsJson?["enabledLanguages"] as JsonArray)?.Select(n => n?.GetValue<string>()).Where(l => l is "ar" or "en").Cast<string>().ToList() ?? ["ar", "en"];
        if (!enabled.Contains("ar"))
        {
            enabled.Insert(0, "ar");
        }

        var sectionArray = new JsonArray();
        JsonObject? faq = null;
        foreach (var (section, schema, data) in chosen)
        {
            var localized = localizer.Localize(schema.Fields, data);
            if (section.Key == SectionSchemas.Faq)
            {
                faq = localized;
            }

            sectionArray.Add(new JsonObject
            {
                ["key"] = section.Key,
                ["anchor"] = schema.Anchor,
                ["data"] = localized,
            });
        }

        return new JsonObject
        {
            ["version"] = current,
            ["preview"] = preview,
            ["lang"] = lang,
            ["dir"] = Languages.Dir(lang),
            ["defaultLanguage"] = settingsJson?["defaultLanguage"]?.GetValue<string>() ?? "ar",
            ["languages"] = new JsonArray(enabled.Select(l => (JsonNode)new JsonObject
            {
                ["code"] = l,
                ["label"] = Languages.Label(l),
                ["dir"] = Languages.Dir(l),
                ["home"] = Languages.HomePath(l),
                ["news"] = Languages.NewsPath(l),
            }).ToArray()),
            ["settings"] = settings,
            ["sections"] = sectionArray,
            ["seo"] = seo.Home(lang, settings, enabled, faq),
            ["generatedAt"] = clock.GetUtcNow().UtcDateTime,
        };
    }
}
