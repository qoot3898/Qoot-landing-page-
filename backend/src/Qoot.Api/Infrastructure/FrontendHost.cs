using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Qoot.Api.Services;

namespace Qoot.Api.Infrastructure;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>Folder holding the built website (frontend/dist). Relative to the content root. Empty turns hosting off.</summary>
    public string DistPath { get; set; } = "wwwroot";

    /// <summary>Extra origins the browser may call from the page (when the API lives elsewhere).</summary>
    public string[] ConnectSources { get; set; } = [];
}

/// <summary>
/// Serves the built website from the API's own origin: the public pages (with their search metadata
/// written into the HTML for crawlers), the news pages and the admin dashboard. Static files are served
/// before this middleware runs; it only answers page routes.
/// </summary>
public sealed class FrontendHostMiddleware(RequestDelegate next, IWebHostEnvironment env, IOptions<FrontendOptions> options, ILogger<FrontendHostMiddleware> logger)
{
    private readonly ConcurrentDictionary<string, string> _templates = new();
    private readonly string? _root = ResolveRoot(env, options.Value.DistPath);

    public async Task InvokeAsync(HttpContext context, PageSeoService seo)
    {
        if (_root is null || !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase)
            || path is "/health" or "/sitemap.xml" or "/robots.txt")
        {
            await next(context);
            return;
        }

        // Canonical trailing slashes.
        if (path is "/en" or "/news" or "/en/news" or "/admin")
        {
            context.Response.Redirect(path + "/" + context.Request.QueryString, permanent: true);
            return;
        }

        var ct = context.RequestAborted;
        if (path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase))
        {
            await ServeAsync(context, "admin/index.html", head: null, lang: "ar", status: 200, cacheForever: false);
            return;
        }

        var lang = path.StartsWith("/en/", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
        var rest = lang == "en" ? path[3..] : path;
        try
        {
            if (rest is "/" or "/index.html")
            {
                await ServeAsync(context, "index.html", SeoBuilder.RenderHead(await seo.HomeAsync(lang, ct)), lang, 200, false);
                return;
            }

            if (rest.Equals("/news/", StringComparison.OrdinalIgnoreCase))
            {
                await ServeAsync(context, "news.html", SeoBuilder.RenderHead(await seo.NewsIndexAsync(lang, ct)), lang, 200, false);
                return;
            }

            if (rest.StartsWith("/news/", StringComparison.OrdinalIgnoreCase))
            {
                var slug = Uri.UnescapeDataString(rest["/news/".Length..].TrimEnd('/'));
                var article = SafeText.IsSlug(slug) ? await seo.ArticleAsync(lang, slug, ct) : null;
                if (article is null)
                {
                    // The page still loads (and shows its "not found" state); search engines are told it does not exist.
                    var notFound = await seo.NewsIndexAsync(lang, ct);
                    notFound["robots"] = "noindex";
                    await ServeAsync(context, "news.html", SeoBuilder.RenderHead(notFound), lang, 404, false);
                    return;
                }

                await ServeAsync(context, "news.html", SeoBuilder.RenderHead(article), lang, 200, false);
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Metadata is an enhancement: if the database is unreachable the page shell still loads and shows its error state.
            logger.LogWarning(ex, "Could not build page metadata for {Path}", path);
            var file = rest.StartsWith("/news/", StringComparison.OrdinalIgnoreCase) ? "news.html" : "index.html";
            await ServeAsync(context, file, head: null, lang, 200, false);
            return;
        }

        await next(context);
    }

    private async Task ServeAsync(HttpContext context, string file, string? head, string lang, int status, bool cacheForever)
    {
        var template = await TemplateAsync(file);
        if (template is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var html = head is null ? template : ReplaceHead(template, head);

        if (lang == "en")
        {
            html = html.Replace("<html lang=\"ar\" dir=\"rtl\"", "<html lang=\"en\" dir=\"ltr\"", StringComparison.Ordinal);
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers[HeaderNames.CacheControl] = cacheForever ? "public, max-age=31536000, immutable" : "no-cache";
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await context.Response.WriteAsync(html, context.RequestAborted);
    }

    /// <summary>
    /// The page templates carry a default &lt;title&gt; between &lt;!--qoot:head--&gt; and &lt;!--/qoot:head--&gt;
    /// (for static hosting); the API swaps that whole region for the page's real metadata.
    /// </summary>
    internal static string ReplaceHead(string template, string head)
    {
        const string open = "<!--qoot:head-->";
        const string close = "<!--/qoot:head-->";
        var start = template.IndexOf(open, StringComparison.Ordinal);
        if (start < 0)
        {
            return template;
        }

        var end = template.IndexOf(close, start, StringComparison.Ordinal);
        var stop = end < 0 ? start + open.Length : end + close.Length;
        return string.Concat(template.AsSpan(0, start), head, template.AsSpan(stop));
    }

    private async Task<string?> TemplateAsync(string file)
    {
        if (!env.IsDevelopment() && _templates.TryGetValue(file, out var cached))
        {
            return cached;
        }

        var full = Path.Combine(_root!, file);
        if (!File.Exists(full))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(full);
        _templates[file] = text;
        return text;
    }

    internal static string? ResolveRoot(IWebHostEnvironment env, string distPath)
    {
        if (string.IsNullOrWhiteSpace(distPath))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.IsPathRooted(distPath) ? distPath : Path.Combine(env.ContentRootPath, distPath));
        return File.Exists(Path.Combine(full, "index.html")) ? full : null;
    }

    public static PhysicalFileProvider? FileProvider(IWebHostEnvironment env, string distPath) =>
        ResolveRoot(env, distPath) is { } root ? new PhysicalFileProvider(root) : null;
}

/// <summary>Security headers for every response; a content security policy for HTML pages.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<FrontendOptions> frontend, IOptions<Media.MediaOptions> media)
{
    private readonly string _pageCsp = BuildCsp(frontend.Value, media.Value);

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            if (path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            var type = context.Response.ContentType ?? "";
            if (type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                // Pages may be framed by this origin only: the dashboard previews drafts in a frame.
                headers.XFrameOptions = "SAMEORIGIN";
                headers.ContentSecurityPolicy = _pageCsp;
                return Task.CompletedTask;
            }

            headers.XFrameOptions = "DENY";
            if (path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
            }
            else if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }

    private static string BuildCsp(FrontendOptions frontend, Media.MediaOptions media)
    {
        var connect = string.Join(' ', new[] { "'self'" }.Concat(frontend.ConnectSources));
        var images = "'self' data: blob:" + (media.PublicBaseUrl.Length > 0 ? " " + media.PublicBaseUrl.TrimEnd('/') : "");
        return $"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src {images}; font-src 'self'; connect-src {connect}; " +
               "media-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'";
    }
}

internal static class JsonObjectExtensions
{
    public static JsonObject OrEmpty(this JsonObject? obj) => obj ?? new JsonObject();
}
