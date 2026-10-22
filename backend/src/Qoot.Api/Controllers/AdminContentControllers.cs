using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Qoot.Api.Content;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;
using Qoot.Api.Services;

namespace Qoot.Api.Controllers;

/// <summary>Base for every dashboard endpoint: signed in, Admin or Editor, JSON, never cached.</summary>
[ApiController]
[Authorize(Policy = Policies.ContentEditor)]
[Produces("application/json")]
public abstract class AdminControllerBase : ControllerBase
{
}

[Route("api/admin/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : AdminControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => dashboard.GetAsync(ct);
}

/// <summary>Editable structure of every section and of the settings, used to build the dashboard forms.</summary>
[Route("api/admin/schemas")]
public sealed class SchemasController : AdminControllerBase
{
    [HttpGet]
    public object Get() => new
    {
        sections = SectionSchemas.All,
        settings = SettingsSchema.Schema,
        news = new
        {
            blocks = new[] { "paragraph", "heading", "list", "quote", "image" },
            maxBlocks = NewsBody.MaxBlocks,
            maxText = NewsBody.MaxText,
        },
    };
}

[Route("api/admin/sections")]
public sealed class SectionsController(SectionService sections) : AdminControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SectionSummaryDto>> List(CancellationToken ct) => sections.ListAsync(ct);

    [HttpGet("{key}")]
    public Task<SectionDetailDto> Get(string key, CancellationToken ct) => sections.GetAsync(key, ct);

    /// <summary>Saves the working draft. Visitors keep seeing the published version until it is published.</summary>
    [HttpPut("{key}/draft")]
    public Task<SectionDetailDto> SaveDraft(string key, [FromBody] SaveSectionRequest request, CancellationToken ct) => sections.SaveDraftAsync(key, request, ct);

    [HttpPost("{key}/publish")]
    public Task<SectionDetailDto> Publish(string key, CancellationToken ct) => sections.PublishAsync(key, ct);

    [HttpPost("{key}/discard")]
    public Task<SectionDetailDto> Discard(string key, CancellationToken ct) => sections.DiscardAsync(key, ct);

    [HttpPut("{key}/visibility")]
    public Task<SectionSummaryDto> Visibility(string key, [FromBody] VisibilityRequest request, CancellationToken ct) => sections.SetVisibilityAsync(key, request.Visible, ct);

    [HttpPut("order")]
    public Task<IReadOnlyList<SectionSummaryDto>> Reorder([FromBody] ReorderRequest request, CancellationToken ct) => sections.ReorderAsync(request.Keys ?? [], ct);

    [HttpPost("publish-all")]
    public Task<IReadOnlyList<SectionSummaryDto>> PublishAll(CancellationToken ct) => sections.PublishAllAsync(ct);
}

/// <summary>The site as it will look once drafts are published (for the dashboard's preview).</summary>
[Route("api/admin/preview")]
public sealed class PreviewController(PublicSiteService site, NewsService news, PageSeoService seo) : AdminControllerBase
{
    [HttpGet("site")]
    public async Task<JsonObject> Site([FromQuery] string? lang, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return (await site.GetAsync(Languages.Normalize(lang), preview: true, ct)).Json;
    }

    [HttpGet("news/{id:int}")]
    public async Task<JsonObject> Article(int id, [FromQuery] string? lang, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var language = Languages.Normalize(lang);
        var article = await news.PreviewAsync(id, language, ct);
        article["seo"] = seo.ArticleSeo(language, (await site.GetAsync(language, preview: true, ct)).Json, article);
        return article;
    }
}

[Route("api/admin/news")]
public sealed class NewsAdminController(NewsService news) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<NewsListItemDto>> List([FromQuery] NewsQuery query, CancellationToken ct) => news.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<NewsDetailDto> Get(int id, CancellationToken ct) => news.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<NewsDetailDto>> Create([FromBody] SaveNewsRequest request, CancellationToken ct)
    {
        var created = await news.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<NewsDetailDto> Update(int id, [FromBody] SaveNewsRequest request, CancellationToken ct) => news.UpdateAsync(id, request, ct);

    [HttpPost("{id:int}/publish")]
    public Task<NewsDetailDto> Publish(int id, [FromBody] PublishNewsRequest? request, CancellationToken ct) => news.PublishAsync(id, request ?? new PublishNewsRequest(null), ct);

    [HttpPost("{id:int}/unpublish")]
    public Task<NewsDetailDto> Unpublish(int id, CancellationToken ct) => news.UnpublishAsync(id, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await news.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/admin/news-categories")]
public sealed class NewsCategoriesController(NewsService news) : AdminControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> List(CancellationToken ct) => news.CategoriesAsync(ct);

    [HttpPost]
    public Task<CategoryDto> Create([FromBody] SaveCategoryRequest request, CancellationToken ct) => news.SaveCategoryAsync(null, request, ct);

    [HttpPut("{id:int}")]
    public Task<CategoryDto> Update(int id, [FromBody] SaveCategoryRequest request, CancellationToken ct) => news.SaveCategoryAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await news.DeleteCategoryAsync(id, ct);
        return NoContent();
    }
}

[Route("api/admin/media")]
public sealed class MediaController(MediaService media) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<MediaDto>> List([FromQuery] MediaQuery query, CancellationToken ct) => media.ListAsync(query, ct);

    [HttpGet("{id:int}")]
    public Task<MediaDto> Get(int id, CancellationToken ct) => media.GetAsync(id, ct);

    /// <summary>Uploads one or more images (multipart field "files"). The type is checked from the file's bytes.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimits.Upload)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public Task<IReadOnlyList<MediaDto>> Upload([FromForm] List<IFormFile> files, [FromForm] string? altAr, [FromForm] string? altEn, CancellationToken ct) =>
        media.UploadAsync(files, altAr, altEn, ct);

    /// <summary>Replaces the file behind a media item; everything using it shows the new picture.</summary>
    [HttpPost("{id:int}/replace")]
    [EnableRateLimiting(RateLimits.Upload)]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<MediaDto> Replace(int id, IFormFile? file, CancellationToken ct) => media.ReplaceAsync(id, file, ct);

    [HttpPut("{id:int}")]
    public Task<MediaDto> Update(int id, [FromBody] UpdateMediaRequest request, CancellationToken ct) => media.UpdateAsync(id, request, ct);

    [HttpGet("{id:int}/usage")]
    public Task<IReadOnlyList<MediaUsageDto>> Usage(int id, CancellationToken ct) => media.UsageAsync(id, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await media.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/admin/messages")]
public sealed class MessagesController(ContactService contacts) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<SubmissionDto>> List([FromQuery] SubmissionQuery query, CancellationToken ct) => contacts.ListAsync(query, ct);

    /// <summary>Opens a request (and marks it read).</summary>
    [HttpGet("{id:int}")]
    public Task<SubmissionDto> Get(int id, CancellationToken ct) => contacts.GetAsync(id, ct);

    [HttpPatch("{id:int}")]
    public Task<SubmissionDto> Update(int id, [FromBody] UpdateSubmissionRequest request, CancellationToken ct) => contacts.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Editors may read the settings (they appear in previews); only administrators change them.</summary>
[Route("api/admin/settings")]
public sealed class SettingsController(SettingsService settings) : AdminControllerBase
{
    [HttpGet]
    public Task<SettingsDto> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut]
    [Authorize(Policy = Policies.Administrator)]
    public Task<SettingsDto> Save([FromBody] SaveSettingsRequest request, CancellationToken ct) => settings.SaveAsync(request, ct);
}

[Route("api/admin/users")]
[Authorize(Policy = Policies.Administrator)]
public sealed class UsersController(UserService users) : AdminControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => users.ListAsync(ct);

    [HttpPost]
    public Task<UserDto> Create([FromBody] CreateUserRequest request, CancellationToken ct) => users.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public Task<UserDto> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        await users.UnlockAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await users.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/admin/audit")]
[Authorize(Policy = Policies.Administrator)]
public sealed class AuditController(DashboardService dashboard) : AdminControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditDto>> List([FromQuery] AuditQuery query, CancellationToken ct) => dashboard.AuditAsync(query, ct);
}
