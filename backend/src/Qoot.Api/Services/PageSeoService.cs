using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Content;
using Qoot.Api.Data;

namespace Qoot.Api.Services;

/// <summary>Search metadata per page, plus the sitemap and robots.txt.</summary>
public sealed class PageSeoService(PublicSiteService site, NewsService news, SeoBuilder builder, QootDbContext db, TimeProvider clock)
{
    public async Task<JsonObject> HomeAsync(string lang, CancellationToken ct)
    {
        var payload = (await site.GetAsync(lang, preview: false, ct)).Json;
        return payload["seo"] as JsonObject ?? [];
    }

    public async Task<JsonObject> NewsIndexAsync(string lang, CancellationToken ct)
    {
        var payload = (await site.GetAsync(lang, preview: false, ct)).Json;
        var (settings, languages) = Context(payload);
        var newsSection = (payload["sections"] as JsonArray)?.FirstOrDefault(s => s?["key"]?.GetValue<string>() == SectionSchemas.News)?["data"];
        var heading = SeoBuilder.Str(newsSection, "title", lang == "ar" ? "آخر الأخبار" : "Latest news");
        var intro = SeoBuilder.Str(newsSection, "intro");
        if (newsSection is null)
        {
            // The section may be hidden on the home page; the news page still uses its published wording.
            var stored = await db.Sections.AsNoTracking().Where(s => s.Key == SectionSchemas.News).Select(s => s.PublishedJson).FirstOrDefaultAsync(ct);
            var localizer = new ContentLocalizer(lang, new Dictionary<int, Domain.MediaAsset>(), null!);
            if (JsonDefaults.Parse(stored) is JsonObject raw)
            {
                heading = localizer.Text(raw["title"]) is { Length: > 0 } h ? h : heading;
                intro = localizer.Text(raw["intro"]);
            }
        }

        return builder.NewsIndex(lang, settings, languages, heading, intro);
    }

    public async Task<JsonObject?> ArticleAsync(string lang, string slug, CancellationToken ct)
    {
        var article = await news.PublicGetAsync(slug, lang, ct);
        if (article is null)
        {
            return null;
        }

        var payload = (await site.GetAsync(lang, preview: false, ct)).Json;
        var (settings, languages) = Context(payload);
        return builder.Article(lang, settings, languages, article);
    }

    public JsonObject ArticleSeo(string lang, JsonObject sitePayload, JsonObject article)
    {
        var (settings, languages) = Context(sitePayload);
        return builder.Article(lang, settings, languages, article);
    }

    public async Task<string> SitemapAsync(CancellationToken ct)
    {
        var payload = (await site.GetAsync("ar", preview: false, ct)).Json;
        var (settings, languages) = Context(payload);
        var root = SeoBuilder.SiteUrl(settings);
        var now = clock.GetUtcNow().UtcDateTime;
        var lastChange = await db.ContentStates.AsNoTracking().Select(s => (DateTime?)s.ChangedAt).FirstOrDefaultAsync(ct) ?? now;
        var articles = await news.Visible(now).OrderByDescending(n => n.PublishedAt).Select(n => new { n.Slug, n.UpdatedAt }).Take(5000).ToListAsync(ct);

        var sb = new StringBuilder();
        using (var xml = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false, Encoding = Encoding.UTF8 }))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
            xml.WriteAttributeString("xmlns", "xhtml", null, "http://www.w3.org/1999/xhtml");

            void Url(Func<string, string> path, DateTime modified)
            {
                foreach (var lang in languages)
                {
                    xml.WriteStartElement("url");
                    xml.WriteElementString("loc", root + path(lang));
                    xml.WriteElementString("lastmod", modified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    foreach (var alt in languages)
                    {
                        xml.WriteStartElement("xhtml", "link", "http://www.w3.org/1999/xhtml");
                        xml.WriteAttributeString("rel", "alternate");
                        xml.WriteAttributeString("hreflang", alt);
                        xml.WriteAttributeString("href", root + path(alt));
                        xml.WriteEndElement();
                    }

                    xml.WriteStartElement("xhtml", "link", "http://www.w3.org/1999/xhtml");
                    xml.WriteAttributeString("rel", "alternate");
                    xml.WriteAttributeString("hreflang", "x-default");
                    xml.WriteAttributeString("href", root + path("ar"));
                    xml.WriteEndElement();
                    xml.WriteEndElement();
                }
            }

            Url(Languages.HomePath, lastChange);
            if (articles.Count > 0)
            {
                Url(Languages.NewsPath, articles.Max(a => a.UpdatedAt));
            }

            foreach (var article in articles)
            {
                Url(l => Languages.ArticlePath(l, article.Slug), article.UpdatedAt);
            }

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        // XmlWriter over a StringBuilder declares UTF-16; the response is UTF-8.
        return sb.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"", StringComparison.Ordinal);
    }

    public async Task<string> RobotsAsync(CancellationToken ct)
    {
        var payload = (await site.GetAsync("ar", preview: false, ct)).Json;
        var root = SeoBuilder.SiteUrl(payload["settings"] as JsonObject ?? []);
        return $"User-agent: *\nAllow: /\nDisallow: /admin/\nDisallow: /api/\n\nSitemap: {root}/sitemap.xml\n";
    }

    private static (JsonObject Settings, List<string> Languages) Context(JsonObject payload)
    {
        var settings = payload["settings"] as JsonObject ?? [];
        var languages = (payload["languages"] as JsonArray)?.Select(l => l?["code"]?.GetValue<string>() ?? "").Where(l => l.Length > 0).ToList() ?? ["ar", "en"];
        return (settings, languages);
    }
}
