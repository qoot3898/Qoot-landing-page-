using System.Net;
using System.Text.Json.Nodes;
using Qoot.Api.Tests.Support;

namespace Qoot.Api.Tests;

public sealed class PublicSiteTests : IClassFixture<QootFactory>
{
    private readonly QootFactory _factory;

    public PublicSiteTests(QootFactory factory) => _factory = factory;

    [Fact]
    public async Task Site_is_seeded_with_every_existing_section_in_page_order()
    {
        using var client = _factory.CreateQootClient();
        var site = await client.SiteAsync("en");

        var keys = site["sections"]!.AsArray().Select(s => s!["key"]!.GetValue<string>()).ToList();
        keys.ShouldBe(["header", "hero", "supplies", "how", "who", "news", "faq", "contact", "visit", "footer"]);
        site["dir"]!.GetValue<string>().ShouldBe("ltr");
        site.Section("hero").Str("titleStart").ShouldBe("Qoot catering");
        site.Section("supplies")["tiles"]!.AsArray().Count.ShouldBe(8);
        site.Section("how")["steps"]!.AsArray().Count.ShouldBe(4);
        site.Section("faq")["items"]!.AsArray().Count.ShouldBe(6);
        site.Section("who")["cities"]!.AsArray().Count.ShouldBe(14);
    }

    [Fact]
    public async Task Arabic_payload_is_right_to_left_with_arabic_text()
    {
        using var client = _factory.CreateQootClient("ar");
        var site = await client.SiteAsync("ar");
        site["dir"]!.GetValue<string>().ShouldBe("rtl");
        site.Section("hero").Str("titleStart").ShouldBe("قوت");
        site.Section("supplies").Str("title").ShouldBe("ما نوفّره");
    }

    [Fact]
    public async Task Pictures_are_resolved_with_alt_text_in_the_page_language()
    {
        using var client = _factory.CreateQootClient();
        var hero = (await client.SiteAsync("en")).Section("hero");
        var image = hero["slides"]![0]!["image"]!;
        image.Str("url").ShouldStartWith("/assets/img/qoot-oranges-sorting-line");
        image.Str("alt").ShouldBe("Oranges moving along a sorting line before packing");
        image["sources"]!.AsArray().Count.ShouldBe(2);

        var arabic = (await client.SiteAsync("ar")).Section("hero")["slides"]![0]!["image"]!;
        arabic.Str("alt").ShouldBe("برتقال على خط الفرز قبل تعبئته");
    }

    [Fact]
    public async Task Private_settings_are_never_sent_to_visitors()
    {
        using var client = _factory.CreateQootClient();
        var site = await client.SiteAsync("en");
        var settings = site["settings"]!.AsObject();
        settings.ContainsKey("notifyEmail").ShouldBeFalse();
        settings.Str("email").ShouldBe("info@qoot.com");
    }

    [Fact]
    public async Task Responses_carry_an_etag_and_answer_304_when_unchanged()
    {
        using var client = _factory.CreateQootClient();
        var first = await client.Http.GetAsync("/api/public/site?lang=en");
        var etag = first.Headers.ETag!.ToString();
        etag.ShouldNotBeNullOrEmpty();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/site?lang=en");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await client.Http.SendAsync(request);
        second.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Seo_metadata_has_hreflang_alternates_and_faq_structured_data()
    {
        using var client = _factory.CreateQootClient();
        var seo = (await client.SiteAsync("en"))["seo"]!;
        seo.Str("canonical").ShouldBe("https://www.qoot.com/en/");
        seo["alternates"]!.AsArray().Select(a => a.Str("hreflang")).ShouldBe(["ar", "en", "x-default"]);
        var graph = seo["jsonLd"]!["@graph"]!.AsArray();
        graph.Select(g => g.Str("@type")).ShouldContain("FAQPage");
        graph.Select(g => g.Str("@type")).ShouldContain("LocalBusiness");
    }

    [Fact]
    public async Task Unknown_api_routes_answer_a_problem_document()
    {
        using var client = _factory.CreateQootClient();
        var response = await client.Http.GetAsync("/api/does-not-exist");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body.Str("code").ShouldBe("not_found");
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        using var client = _factory.CreateQootClient();
        var response = await client.Http.GetAsync("/api/public/site?lang=ar");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.Contains("Referrer-Policy").ShouldBeTrue();
        response.Headers.Contains("Server").ShouldBeFalse();
    }

    [Fact]
    public async Task Sitemap_and_robots_are_generated()
    {
        using var client = _factory.CreateQootClient();
        var sitemap = await client.Http.GetStringAsync("/sitemap.xml");
        sitemap.ShouldContain("<loc>https://www.qoot.com/</loc>");
        sitemap.ShouldContain("<loc>https://www.qoot.com/en/</loc>");
        sitemap.ShouldContain("hreflang=\"x-default\"");
        var robots = await client.Http.GetStringAsync("/robots.txt");
        robots.ShouldContain("Disallow: /admin/");
        robots.ShouldContain("Sitemap: https://www.qoot.com/sitemap.xml");
    }

    [Fact]
    public async Task Health_endpoint_reports_the_database()
    {
        using var client = _factory.CreateQootClient();
        (await client.Http.GetStringAsync("/health")).ShouldBe("Healthy");
    }
}
