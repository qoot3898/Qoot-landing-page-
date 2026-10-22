using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Qoot.Api.Content;
using Qoot.Api.Infrastructure;
using Qoot.Api.Services;

namespace Qoot.Api.Controllers;

/// <summary>Read-only content for visitors. Only published, visible content is ever returned here.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting(RateLimits.Public)]
[Produces("application/json")]
public sealed class PublicController(PublicSiteService site, NewsService news, PageSeoService seo, ContactService contacts) : ControllerBase
{
    /// <summary>The whole page for one language: settings, visible sections in order, and search metadata.</summary>
    [HttpGet("site")]
    public async Task<IActionResult> Site([FromQuery] string? lang, CancellationToken ct)
    {
        var language = Languages.Normalize(lang);
        var payload = await site.GetAsync(language, preview: false, ct);
        return CachedJson(payload.Json);
    }

    /// <summary>Published articles, newest first (articles given a higher priority come first).</summary>
    [HttpGet("news")]
    public async Task<IActionResult> News([FromQuery] string? lang, [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? category, CancellationToken ct)
    {
        var language = Languages.Normalize(lang);
        var result = await news.PublicListAsync(language, category, page, pageSize, ct);
        var body = new JsonObject
        {
            ["items"] = new JsonArray(result.Items.Select(i => (JsonNode)i).ToArray()),
            ["page"] = result.Page,
            ["pageSize"] = result.PageSize,
            ["total"] = result.Total,
            ["totalPages"] = result.TotalPages,
            ["seo"] = await seo.NewsIndexAsync(language, ct),
        };
        return CachedJson(body);
    }

    [HttpGet("news/categories")]
    public async Task<IActionResult> Categories([FromQuery] string? lang, CancellationToken ct) =>
        CachedJson(new JsonObject { ["items"] = new JsonArray((await news.PublicCategoriesAsync(Languages.Normalize(lang), ct)).Select(c => (JsonNode)c).ToArray()) });

    /// <summary>One published article. Drafts, scheduled and unknown articles all answer 404.</summary>
    [HttpGet("news/{slug}")]
    public async Task<IActionResult> Article(string slug, [FromQuery] string? lang, CancellationToken ct)
    {
        var language = Languages.Normalize(lang);
        if (!SafeText.IsSlug(slug))
        {
            throw ApiError.NotFound();
        }

        var article = await news.PublicGetAsync(slug, language, ct) ?? throw ApiError.NotFound();
        var payload = (await site.GetAsync(language, preview: false, ct)).Json;
        article["seo"] = seo.ArticleSeo(language, payload, article);
        return CachedJson(article);
    }

    /// <summary>Stores a Contact us message. No authentication; rate limited per address; a hidden field catches bots.</summary>
    [HttpPost("contact")]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(RateLimits.Contact)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Contact([FromBody] ContactRequest request, CancellationToken ct)
    {
        await contacts.SubmitAsync(request, ct);
        return Accepted(new { ok = true });
    }

    /// <summary>JSON with an ETag from its content: browsers revalidate every time and get 304 while nothing changed.</summary>
    private IActionResult CachedJson(JsonNode body)
    {
        var json = body.ToJsonString(JsonDefaults.Output);
        var tag = "W/\"" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..20] + "\"";
        Response.Headers[HeaderNames.ETag] = tag;
        Response.Headers[HeaderNames.CacheControl] = "no-cache";
        Response.Headers[HeaderNames.Vary] = "Accept-Language";
        if (Request.Headers.IfNoneMatch.ToString().Split(',').Select(s => s.Trim()).Contains(tag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Content(json, "application/json; charset=utf-8");
    }
}

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SeoFilesController(PageSeoService seo) : ControllerBase
{
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=3600";
        return Content(await seo.SitemapAsync(ct), "application/xml; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots(CancellationToken ct)
    {
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=3600";
        return Content(await seo.RobotsAsync(ct), "text/plain; charset=utf-8");
    }
}
