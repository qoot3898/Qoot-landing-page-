using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qoot.Api.Services;

/// <summary>
/// Builds the search and sharing metadata for each page type in one place. The website applies it in
/// the browser, and the API writes the same tags into the HTML it serves, so crawlers that do not run
/// JavaScript (link previews) still see the right title, description and image.
/// </summary>
public sealed class SeoBuilder
{
    private static readonly string[] DayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    public JsonObject Home(string lang, JsonObject settings, IReadOnlyList<string> languages, JsonObject? faq)
    {
        var site = SiteUrl(settings);
        var title = Str(settings, "seoTitle", Str(settings, "siteName", "Qoot"));
        var description = Str(settings, "seoDescription");
        var url = site + Languages.HomePath(lang);
        var graph = BaseGraph(lang, settings, site, url, title, description);
        if (faq?["items"] is JsonArray items && items.Count > 0)
        {
            graph.Add(new JsonObject
            {
                ["@type"] = "FAQPage",
                ["@id"] = url + "#faq",
                ["inLanguage"] = lang,
                ["mainEntity"] = new JsonArray(items.Select(i => (JsonNode)new JsonObject
                {
                    ["@type"] = "Question",
                    ["name"] = Str(i, "question"),
                    ["acceptedAnswer"] = new JsonObject { ["@type"] = "Answer", ["text"] = Str(i, "answer") },
                }).ToArray()),
            });
        }

        return Page(lang, settings, languages, title, description, url, l => site + Languages.HomePath(l), "website", graph, Picture(settings, "ogImage"), Str(settings, "ogImageAlt"));
    }

    public JsonObject NewsIndex(string lang, JsonObject settings, IReadOnlyList<string> languages, string heading, string intro)
    {
        var site = SiteUrl(settings);
        var name = Str(settings, "siteName", "Qoot");
        var title = $"{heading} | {name}";
        var description = intro.Length > 0 ? intro : Str(settings, "seoDescription");
        var url = site + Languages.NewsPath(lang);
        var graph = BaseGraph(lang, settings, site, url, title, description);
        return Page(lang, settings, languages, title, description, url, l => site + Languages.NewsPath(l), "website", graph, Picture(settings, "ogImage"), Str(settings, "ogImageAlt"));
    }

    public JsonObject Article(string lang, JsonObject settings, IReadOnlyList<string> languages, JsonObject article)
    {
        var site = SiteUrl(settings);
        var slug = Str(article, "slug");
        var name = Str(settings, "siteName", "Qoot");
        var title = $"{Str(article, "metaTitle", Str(article, "title"))} | {name}";
        var description = Str(article, "metaDescription");
        var url = site + Languages.ArticlePath(lang, slug);
        var image = article["image"] as JsonObject ?? Picture(settings, "ogImage");
        var imageUrl = image is null ? null : Absolute(site, Str(image, "url"));
        var graph = BaseGraph(lang, settings, site, url, title, description);
        var newsArticle = new JsonObject
        {
            ["@type"] = "NewsArticle",
            ["@id"] = url + "#article",
            ["headline"] = Str(article, "title"),
            ["description"] = description,
            ["inLanguage"] = Str(article, "contentLang", lang),
            ["mainEntityOfPage"] = url,
            ["datePublished"] = article["publishedAt"]?.DeepClone(),
            ["dateModified"] = article["updatedAt"]?.DeepClone() ?? article["publishedAt"]?.DeepClone(),
            ["publisher"] = new JsonObject { ["@id"] = site + "/#organization" },
            ["author"] = new JsonObject { ["@id"] = site + "/#organization" },
        };
        if (imageUrl is not null)
        {
            newsArticle["image"] = imageUrl;
        }

        graph.Add(newsArticle);
        var seo = Page(lang, settings, languages, title, description, url, l => site + Languages.ArticlePath(l, slug), "article", graph, image, image is null ? "" : Str(image, "alt"));
        ((JsonObject)seo["og"]!)["article:published_time"] = article["publishedAt"]?.DeepClone();
        return seo;
    }

    /// <summary>The same metadata as HTML tags, for the server-rendered page shell.</summary>
    public static string RenderHead(JsonObject seo)
    {
        static string A(string? v) => WebUtility.HtmlEncode(v ?? "");
        var sb = new StringBuilder();
        sb.Append("<title>").Append(A(Str(seo, "title"))).Append("</title>\n");
        sb.Append("<meta name=\"description\" content=\"").Append(A(Str(seo, "description"))).Append("\">\n");
        sb.Append("<meta name=\"robots\" content=\"").Append(A(Str(seo, "robots"))).Append("\">\n");
        sb.Append("<link rel=\"canonical\" href=\"").Append(A(Str(seo, "canonical"))).Append("\">\n");
        if (seo["alternates"] is JsonArray alternates)
        {
            foreach (var alt in alternates)
            {
                sb.Append("<link rel=\"alternate\" hreflang=\"").Append(A(Str(alt, "hreflang"))).Append("\" href=\"").Append(A(Str(alt, "href"))).Append("\">\n");
            }
        }

        if (seo["og"] is JsonObject og)
        {
            foreach (var (key, value) in og)
            {
                if (value is null)
                {
                    continue;
                }

                sb.Append("<meta property=\"og:").Append(A(key)).Append("\" content=\"").Append(A(ValueText(value))).Append("\">\n");
            }
        }

        if (seo["twitter"] is JsonObject tw)
        {
            foreach (var (key, value) in tw)
            {
                if (value is not null)
                {
                    sb.Append("<meta name=\"twitter:").Append(A(key)).Append("\" content=\"").Append(A(ValueText(value))).Append("\">\n");
                }
            }
        }

        if (seo["jsonLd"] is JsonNode ld)
        {
            // The default encoder escapes <, > and &, so the JSON cannot close the script element.
            sb.Append("<script type=\"application/ld+json\" data-qoot-ld>").Append(ld.ToJsonString(JsonSerializerOptions.Default)).Append("</script>\n");
        }

        return sb.ToString();
    }

    private static JsonObject Page(
        string lang, JsonObject settings, IReadOnlyList<string> languages, string title, string description, string url,
        Func<string, string> urlFor, string ogType, JsonArray graph, JsonObject? image, string imageAlt)
    {
        var site = SiteUrl(settings);
        var alternates = new JsonArray();
        foreach (var l in languages)
        {
            alternates.Add(new JsonObject { ["hreflang"] = l, ["href"] = urlFor(l) });
        }

        alternates.Add(new JsonObject { ["hreflang"] = "x-default", ["href"] = urlFor("ar") });
        var imageUrl = image is null ? null : Absolute(site, Str(image, "url"));
        var locale = lang == "ar" ? "ar_LY" : "en_GB";
        var og = new JsonObject
        {
            ["type"] = ogType,
            ["site_name"] = Str(settings, "siteName", "Qoot"),
            ["title"] = title,
            ["description"] = description,
            ["url"] = url,
            ["locale"] = locale,
        };
        foreach (var l in languages.Where(l => l != lang))
        {
            og["locale:alternate"] = l == "ar" ? "ar_LY" : "en_GB";
        }

        if (imageUrl is not null)
        {
            og["image"] = imageUrl;
            og["image:width"] = image!["width"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture);
            og["image:height"] = image["height"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture);
            og["image:alt"] = imageAlt;
        }

        return new JsonObject
        {
            ["title"] = title,
            ["description"] = description,
            ["canonical"] = url,
            ["robots"] = "index, follow, max-image-preview:large",
            ["lang"] = lang,
            ["alternates"] = alternates,
            ["og"] = og,
            ["twitter"] = new JsonObject
            {
                ["card"] = imageUrl is null ? "summary" : "summary_large_image",
                ["title"] = title,
                ["description"] = description,
                ["image"] = imageUrl,
            },
            ["jsonLd"] = new JsonObject { ["@context"] = "https://schema.org", ["@graph"] = graph },
        };
    }

    private static JsonArray BaseGraph(string lang, JsonObject settings, string site, string url, string title, string description)
    {
        var name = Str(settings, "siteName", "Qoot");
        var email = Str(settings, "email");
        var phone = Str(settings, "phone");
        var logo = settings[lang == "ar" ? "logoAr" : "logoEn"] as JsonObject;
        var socials = (settings["socialLinks"] as JsonArray)?.Select(s => Str(s, "url")).Where(u => u.Length > 0).Select(u => (JsonNode)u).ToArray() ?? [];
        var org = new JsonObject
        {
            ["@type"] = "Organization",
            ["@id"] = site + "/#organization",
            ["name"] = name,
            ["url"] = site + "/",
        };
        if (logo is not null)
        {
            org["logo"] = site + "/assets/logo/qoot-icon-512.png";
        }

        if (email.Length > 0)
        {
            org["email"] = email;
        }

        if (phone.Length > 0)
        {
            org["telephone"] = phone;
        }

        if (socials.Length > 0)
        {
            org["sameAs"] = new JsonArray(socials);
        }

        var business = new JsonObject
        {
            ["@type"] = "LocalBusiness",
            ["@id"] = site + "/#business",
            ["name"] = name,
            ["parentOrganization"] = new JsonObject { ["@id"] = site + "/#organization" },
            ["url"] = url,
            ["description"] = Str(settings, "seoDescription"),
            ["address"] = new JsonObject { ["@type"] = "PostalAddress", ["addressLocality"] = "Benghazi", ["addressCountry"] = "LY", ["streetAddress"] = Str(settings, "address") },
            ["areaServed"] = new JsonArray(new JsonObject { ["@type"] = "Country", ["name"] = "Libya" }),
            ["knowsLanguage"] = new JsonArray("ar", "en"),
        };
        if (email.Length > 0)
        {
            business["email"] = email;
        }

        if (phone.Length > 0)
        {
            business["telephone"] = phone;
        }

        if (Str(settings, "mapsUrl") is { Length: > 0 } maps)
        {
            business["hasMap"] = maps;
        }

        if (Picture(settings, "ogImage") is { } og)
        {
            business["image"] = Absolute(site, Str(og, "url"));
        }

        if (settings["openDays"] is JsonArray days && days.Count > 0 && Str(settings, "opensAt").Length > 0)
        {
            business["openingHoursSpecification"] = new JsonArray(new JsonObject
            {
                ["@type"] = "OpeningHoursSpecification",
                ["dayOfWeek"] = new JsonArray(days.Select(d => (JsonNode)DayNames[Math.Clamp(d!.GetValue<int>(), 0, 6)]).ToArray()),
                ["opens"] = Str(settings, "opensAt"),
                ["closes"] = Str(settings, "closesAt"),
            });
        }

        return new JsonArray(
            org,
            business,
            new JsonObject
            {
                ["@type"] = "WebSite",
                ["@id"] = site + "/#website",
                ["url"] = site + "/",
                ["name"] = name,
                ["inLanguage"] = new JsonArray("ar", "en"),
                ["publisher"] = new JsonObject { ["@id"] = site + "/#organization" },
            },
            new JsonObject
            {
                ["@type"] = "WebPage",
                ["@id"] = url + "#webpage",
                ["url"] = url,
                ["name"] = title,
                ["description"] = description,
                ["inLanguage"] = lang,
                ["isPartOf"] = new JsonObject { ["@id"] = site + "/#website" },
            });
    }

    public static string SiteUrl(JsonObject settings)
    {
        var url = Str(settings, "siteUrl").TrimEnd('/');
        return url.Length > 0 ? url : "https://www.qoot.com";
    }

    private static string Absolute(string site, string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? url : site + (url.StartsWith('/') ? url : "/" + url);

    private static JsonObject? Picture(JsonObject settings, string key) => settings[key] as JsonObject;

    private static string ValueText(JsonNode node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node.ToJsonString().Trim('"');

    internal static string Str(JsonNode? node, string key, string fallback = "")
    {
        var value = node?[key];
        return value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : fallback;
    }
}
