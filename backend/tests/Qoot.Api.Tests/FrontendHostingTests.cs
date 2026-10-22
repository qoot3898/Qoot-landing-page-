using System.Net;
using Qoot.Api.Infrastructure;
using Qoot.Api.Tests.Support;

namespace Qoot.Api.Tests;

/// <summary>
/// The API serving the built website: page routes, the search metadata written into the HTML for
/// crawlers, the right language and direction, 404s for unknown or unpublished articles, the dashboard
/// shell, cache headers and the framing policy.
/// </summary>
public sealed class FrontendHostingTests : IDisposable
{
    private readonly string _dist = Path.Combine(Path.GetTempPath(), "qoot-dist-" + Guid.NewGuid().ToString("N"));
    private readonly QootFactory _factory;

    private const string Template =
        "<!doctype html>\n<html lang=\"ar\" dir=\"rtl\">\n<head>\n<!--qoot:head--><title>Qoot قوت</title><!--/qoot:head-->\n<script src=\"/boot.js\"></script>\n</head>\n<body><div id=\"app\"></div></body>\n</html>";

    public FrontendHostingTests()
    {
        Directory.CreateDirectory(Path.Combine(_dist, "admin"));
        Directory.CreateDirectory(Path.Combine(_dist, "static"));
        File.WriteAllText(Path.Combine(_dist, "index.html"), Template.Replace("</body>", "<!--home--></body>", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(_dist, "news.html"), Template.Replace("</body>", "<!--news--></body>", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(_dist, "admin", "index.html"), "<!doctype html><html lang=\"ar\" dir=\"rtl\"><head><meta name=\"robots\" content=\"noindex, nofollow\"><title>Qoot dashboard</title></head><body><div id=\"root\"></div></body></html>");
        File.WriteAllText(Path.Combine(_dist, "static", "app-1a2b3c.js"), "console.log('qoot');");
        File.WriteAllText(Path.Combine(_dist, "boot.js"), "/* boot */");
        _factory = new QootFactory { Overrides = { ["Frontend:DistPath"] = _dist } };
    }

    public void Dispose()
    {
        _factory.Dispose();
        try
        {
            Directory.Delete(_dist, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    [Fact]
    public async Task Home_pages_carry_their_metadata_language_and_direction()
    {
        using var visitor = _factory.CreateQootClient();
        var ar = await visitor.Http.GetAsync("/");
        ar.StatusCode.ShouldBe(HttpStatusCode.OK);
        var arHtml = await ar.Content.ReadAsStringAsync();
        arHtml.ShouldContain("<html lang=\"ar\" dir=\"rtl\"");
        arHtml.ShouldContain("<!--home-->");
        arHtml.ShouldContain("<link rel=\"canonical\" href=\"https://www.qoot.com/\">");
        arHtml.ShouldContain("hreflang=\"en\"");
        arHtml.ShouldContain("application/ld+json");
        arHtml.ShouldNotContain("<title>Qoot قوت</title>");
        arHtml.ShouldNotContain("qoot:head");
        ar.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        ar.Headers.CacheControl!.NoCache.ShouldBeTrue();

        var en = await visitor.Http.GetStringAsync("/en/");
        en.ShouldContain("<html lang=\"en\" dir=\"ltr\"");
        en.ShouldContain("<link rel=\"canonical\" href=\"https://www.qoot.com/en/\">");
        en.ShouldContain("<title>Qoot | Catering");
    }

    [Theory]
    [InlineData("/en", "/en/")]
    [InlineData("/news", "/news/")]
    [InlineData("/en/news", "/en/news/")]
    [InlineData("/admin", "/admin/")]
    public async Task Short_page_addresses_redirect_to_the_canonical_one(string path, string target)
    {
        using var visitor = _factory.CreateQootClient();
        var response = await visitor.Http.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.ShouldBe(target);
    }

    [Fact]
    public async Task Articles_get_their_own_metadata_and_unpublished_ones_are_404_for_visitors()
    {
        using var admin = await _factory.SignInAsync();
        using var visitor = _factory.CreateQootClient();
        var (_, created) = await admin.PostAsync("/api/admin/news", new
        {
            slug = "new-bakery-line",
            titleEn = "A new bakery line",
            titleAr = "خط مخبوزات جديد",
            summaryEn = "Fresh bread every morning.",
            summaryAr = "خبز طازج كل صباح.",
            bodyEn = Array.Empty<object>(),
            bodyAr = Array.Empty<object>(),
            isFeatured = false,
            priority = 0,
        });
        var id = created!["id"]!.GetValue<int>();

        var draft = await visitor.Http.GetAsync("/en/news/new-bakery-line");
        draft.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var draftHtml = await draft.Content.ReadAsStringAsync();
        draftHtml.ShouldContain("noindex");
        draftHtml.ShouldNotContain("A new bakery line");

        await admin.PostAsync($"/api/admin/news/{id}/publish", new { });
        var page = await visitor.Http.GetAsync("/en/news/new-bakery-line");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync();
        html.ShouldContain("<!--news-->");
        html.ShouldContain("<title>A new bakery line | Qoot Catering &amp; Supplies</title>");
        html.ShouldContain("<link rel=\"canonical\" href=\"https://www.qoot.com/en/news/new-bakery-line\">");
        html.ShouldContain("\"@type\":\"NewsArticle\"");

        var ar = await visitor.Http.GetStringAsync("/news/new-bakery-line");
        ar.ShouldContain("<html lang=\"ar\" dir=\"rtl\"");
        ar.ShouldContain("خط مخبوزات جديد");

        (await visitor.Http.GetAsync("/news/%3Cscript%3E")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await visitor.Http.GetAsync("/en/news/")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_routes_get_the_dashboard_shell_and_files_are_cached_by_kind()
    {
        using var visitor = _factory.CreateQootClient();
        var admin = await visitor.Http.GetAsync("/admin/news/12");
        admin.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.Content.ReadAsStringAsync()).ShouldContain("<div id=\"root\">");

        var asset = await visitor.Http.GetAsync("/static/app-1a2b3c.js");
        asset.StatusCode.ShouldBe(HttpStatusCode.OK);
        asset.Headers.CacheControl!.ToString().ShouldContain("immutable");

        (await visitor.Http.GetAsync("/no-such-page")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await visitor.Http.GetAsync("/api/no-such-endpoint")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pages_may_be_framed_by_the_dashboard_only_and_the_api_never()
    {
        using var visitor = _factory.CreateQootClient();
        var page = await visitor.Http.GetAsync("/en/");
        page.Headers.GetValues("X-Frame-Options").ShouldContain("SAMEORIGIN");
        var csp = string.Join(' ', page.Headers.GetValues("Content-Security-Policy"));
        csp.ShouldContain("frame-ancestors 'self'");
        csp.ShouldContain("script-src 'self'");
        csp.ShouldNotContain("unsafe-eval");

        var api = await visitor.Http.GetAsync("/api/public/site?lang=en");
        api.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
    }

    [Theory]
    [InlineData("<!--qoot:head--><title>x</title><!--/qoot:head--><p>", "<title>y</title><p>")]
    [InlineData("<!--qoot:head--><p>", "<title>y</title><p>")]
    [InlineData("<p>", "<p>")]
    public void The_head_region_is_replaced_whole(string template, string expected)
    {
        FrontendHostMiddleware.ReplaceHead(template, "<title>y</title>").ShouldBe(expected);
    }
}
