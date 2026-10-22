using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Content;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;
using Qoot.Api.Media;

namespace Qoot.Api.Services;

public sealed record CategoryRef(int Id, string Slug, string NameAr, string NameEn);

public sealed record NewsListItemDto(
    int Id,
    string Slug,
    string TitleAr,
    string TitleEn,
    string Status,
    DateTime? PublishedAt,
    bool IsFeatured,
    int Priority,
    CategoryRef? Category,
    string? ImageUrl,
    DateTime UpdatedAt,
    bool HasArabic,
    bool HasEnglish);

public sealed record NewsDetailDto(
    int Id,
    string Slug,
    string TitleAr,
    string TitleEn,
    string SummaryAr,
    string SummaryEn,
    JsonNode BodyAr,
    JsonNode BodyEn,
    string MetaTitleAr,
    string MetaTitleEn,
    string MetaDescriptionAr,
    string MetaDescriptionEn,
    int? FeaturedImageId,
    JsonObject? FeaturedImage,
    int? CategoryId,
    string Status,
    DateTime? PublishedAt,
    bool IsFeatured,
    int Priority,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid ConcurrencyStamp);

public sealed record SaveNewsRequest(
    string? Slug,
    string? TitleAr,
    string? TitleEn,
    string? SummaryAr,
    string? SummaryEn,
    JsonNode? BodyAr,
    JsonNode? BodyEn,
    string? MetaTitleAr,
    string? MetaTitleEn,
    string? MetaDescriptionAr,
    string? MetaDescriptionEn,
    int? FeaturedImageId,
    int? CategoryId,
    bool IsFeatured,
    int Priority,
    DateTime? PublishedAt,
    Guid? ConcurrencyStamp);

public sealed record PublishNewsRequest(DateTime? PublishAt);

public sealed record NewsQuery(string? Search, string? Status, int? CategoryId, string? Sort, int? Page, int? PageSize);

public sealed record CategoryDto(int Id, string Slug, string NameAr, string NameEn, int SortOrder, int ArticleCount);

public sealed record SaveCategoryRequest(string? Slug, string? NameAr, string? NameEn, int SortOrder);

public sealed class NewsService(
    QootDbContext db,
    ContentVersion version,
    AuditService audit,
    ICurrentUser user,
    TimeProvider clock,
    MediaUrls urls)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ dashboard

    public async Task<PagedResult<NewsListItemDto>> ListAsync(NewsQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(q.Page, q.PageSize);
        var now = Now;
        var query = db.News.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(n => EF.Functions.Like(n.TitleAr, term) || EF.Functions.Like(n.TitleEn, term) || EF.Functions.Like(n.Slug, term));
        }

        query = q.Status switch
        {
            "draft" => query.Where(n => n.Status == NewsStatus.Draft),
            "published" => query.Where(n => n.Status == NewsStatus.Published && n.PublishedAt <= now),
            "scheduled" => query.Where(n => n.Status == NewsStatus.Published && n.PublishedAt > now),
            "featured" => query.Where(n => n.IsFeatured),
            _ => query,
        };

        if (q.CategoryId is { } categoryId)
        {
            query = query.Where(n => n.CategoryId == categoryId);
        }

        query = q.Sort switch
        {
            "oldest" => query.OrderBy(n => n.PublishedAt ?? n.CreatedAt).ThenBy(n => n.Id),
            "title" => query.OrderBy(n => n.TitleEn == "" ? n.TitleAr : n.TitleEn).ThenBy(n => n.Id),
            "updated" => query.OrderByDescending(n => n.UpdatedAt).ThenByDescending(n => n.Id),
            "priority" => query.OrderByDescending(n => n.Priority).ThenByDescending(n => n.PublishedAt ?? n.CreatedAt).ThenByDescending(n => n.Id),
            _ => query.OrderByDescending(n => n.PublishedAt ?? n.CreatedAt).ThenByDescending(n => n.Id),
        };

        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * size).Take(size)
            .Select(n => new
            {
                n.Id, n.Slug, n.TitleAr, n.TitleEn, n.Status, n.PublishedAt, n.IsFeatured, n.Priority, n.UpdatedAt,
                n.SummaryAr, n.SummaryEn, n.BodyArJson, n.BodyEnJson,
                Category = n.Category == null ? null : new CategoryRef(n.Category.Id, n.Category.Slug, n.Category.NameAr, n.Category.NameEn),
                Image = n.FeaturedImage,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new NewsListItemDto(
            r.Id, r.Slug, r.TitleAr, r.TitleEn, StatusOf(r.Status, r.PublishedAt, now), r.PublishedAt, r.IsFeatured, r.Priority, r.Category,
            r.Image is null ? null : urls.Url(r.Image), r.UpdatedAt,
            r.TitleAr.Length > 0 && (r.SummaryAr.Length > 0 || r.BodyArJson.Length > 2),
            r.TitleEn.Length > 0 && (r.SummaryEn.Length > 0 || r.BodyEnJson.Length > 2))).ToList();
        return new PagedResult<NewsListItemDto>(items, page, size, total);
    }

    public async Task<NewsDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var article = await db.News.Include(n => n.FeaturedImage).FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiError.NotFound();
        return ToDetail(article);
    }

    public async Task<NewsDetailDto> CreateAsync(SaveNewsRequest request, CancellationToken ct)
    {
        var article = new NewsArticle { Slug = "", CreatedAt = Now, CreatedById = user.Id, Status = NewsStatus.Draft };
        await ApplyAsync(article, request, ct);
        db.News.Add(article);
        audit.Record(AuditActions.NewsCreated, "news", article.Slug, $"Created \"{Title(article)}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(article.Id, ct);
    }

    public async Task<NewsDetailDto> UpdateAsync(int id, SaveNewsRequest request, CancellationToken ct)
    {
        var article = await db.News.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiError.NotFound();
        if (request.ConcurrencyStamp is { } stamp)
        {
            SectionService.EnsureStamp(article.ConcurrencyStamp, stamp);
        }

        await ApplyAsync(article, request, ct);
        if (article.Status == NewsStatus.Published)
        {
            await version.MarkChangedAsync(ct);
        }

        audit.Record(AuditActions.NewsUpdated, "news", article.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Edited \"{Title(article)}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(article.Id, ct);
    }

    public async Task<NewsDetailDto> PublishAsync(int id, PublishNewsRequest request, CancellationToken ct)
    {
        var article = await db.News.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiError.NotFound();
        if (article.TitleAr.Length == 0 && article.TitleEn.Length == 0)
        {
            throw ApiError.BadRequest("publish_needs_title");
        }

        article.Status = NewsStatus.Published;
        article.PublishedAt = request.PublishAt?.ToUniversalTime() ?? article.PublishedAt ?? Now;
        article.UpdatedAt = Now;
        article.UpdatedById = user.Id;
        article.ConcurrencyStamp = Guid.NewGuid();
        await version.MarkChangedAsync(ct);
        var when = article.PublishedAt > Now ? $" (scheduled for {article.PublishedAt:u})" : "";
        audit.Record(AuditActions.NewsPublished, "news", article.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Published \"{Title(article)}\"{when}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<NewsDetailDto> UnpublishAsync(int id, CancellationToken ct)
    {
        var article = await db.News.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiError.NotFound();
        article.Status = NewsStatus.Draft;
        article.UpdatedAt = Now;
        article.UpdatedById = user.Id;
        article.ConcurrencyStamp = Guid.NewGuid();
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.NewsUnpublished, "news", article.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Unpublished \"{Title(article)}\"");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var article = await db.News.FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiError.NotFound();
        db.News.Remove(article);
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.NewsDeleted, "news", article.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Deleted \"{Title(article)}\"");
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(NewsArticle article, SaveNewsRequest r, CancellationToken ct)
    {
        var ctx = new ContentValidationContext();
        var errors = ctx.Errors;
        var mediaRefs = ctx.MediaRefs;

        string Text(string? value, string field, int max, bool multiline = false)
        {
            var clean = SafeText.Clean(value, multiline);
            if (clean.Length > max)
            {
                errors.Add(field, "too_long", max);
            }

            return clean;
        }

        var titleAr = Text(r.TitleAr, "titleAr", 200);
        var titleEn = Text(r.TitleEn, "titleEn", 200);
        if (titleAr.Length == 0 && titleEn.Length == 0)
        {
            errors.Add("titleAr", "title_required");
        }

        var summaryAr = Text(r.SummaryAr, "summaryAr", 600, multiline: true);
        var summaryEn = Text(r.SummaryEn, "summaryEn", 600, multiline: true);
        var metaTitleAr = Text(r.MetaTitleAr, "metaTitleAr", 200);
        var metaTitleEn = Text(r.MetaTitleEn, "metaTitleEn", 200);
        var metaDescAr = Text(r.MetaDescriptionAr, "metaDescriptionAr", 320, multiline: false);
        var metaDescEn = Text(r.MetaDescriptionEn, "metaDescriptionEn", 320, multiline: false);
        var bodyAr = NewsBody.Validate(r.BodyAr, "bodyAr", errors, mediaRefs);
        var bodyEn = NewsBody.Validate(r.BodyEn, "bodyEn", errors, mediaRefs);

        if (r.Priority is < -100 or > 100)
        {
            errors.Add("priority", "invalid_number", -100, 100);
        }

        if (r.FeaturedImageId is > 0)
        {
            mediaRefs.Add(new MediaRef(r.FeaturedImageId.Value, "featuredImageId", AllowDrawings: false));
        }

        if (r.CategoryId is { } categoryId && !await db.NewsCategories.AnyAsync(c => c.Id == categoryId, ct))
        {
            errors.Add("categoryId", "category_not_found");
        }

        var slug = (r.Slug ?? "").Trim().ToLowerInvariant();
        if (slug.Length == 0)
        {
            slug = article.Slug.Length > 0 ? article.Slug : await UniqueSlugAsync(SafeText.Slugify(titleEn), article.Id, ct);
        }
        else if (!SafeText.IsSlug(slug))
        {
            errors.Add("slug", "invalid_slug");
        }
        else if (await db.News.AnyAsync(n => n.Slug == slug && n.Id != article.Id, ct))
        {
            errors.Add("slug", "slug_taken");
        }

        await MediaRefChecker.CheckAsync(db, ctx, ct);
        errors.ThrowIfAny();

        article.Slug = slug;
        article.TitleAr = titleAr;
        article.TitleEn = titleEn;
        article.SummaryAr = summaryAr;
        article.SummaryEn = summaryEn;
        article.BodyArJson = bodyAr.ToJsonString();
        article.BodyEnJson = bodyEn.ToJsonString();
        article.MetaTitleAr = metaTitleAr;
        article.MetaTitleEn = metaTitleEn;
        article.MetaDescriptionAr = metaDescAr;
        article.MetaDescriptionEn = metaDescEn;
        article.FeaturedImageId = r.FeaturedImageId is > 0 ? r.FeaturedImageId : null;
        article.CategoryId = r.CategoryId;
        article.IsFeatured = r.IsFeatured;
        article.Priority = r.Priority;
        if (r.PublishedAt is { } publishedAt)
        {
            article.PublishedAt = publishedAt.ToUniversalTime();
        }

        article.UpdatedAt = Now;
        article.UpdatedById = user.Id;
        article.ConcurrencyStamp = Guid.NewGuid();
    }

    private async Task<string> UniqueSlugAsync(string baseSlug, int excludeId, CancellationToken ct)
    {
        if (baseSlug.Length < 3)
        {
            baseSlug = $"news-{Now:yyyyMMdd}";
        }

        var slug = baseSlug;
        for (var i = 2; await db.News.AnyAsync(n => n.Slug == slug && n.Id != excludeId, ct); i++)
        {
            slug = $"{baseSlug}-{i}";
        }

        return slug;
    }

    private NewsDetailDto ToDetail(NewsArticle a) => new(
        a.Id, a.Slug, a.TitleAr, a.TitleEn, a.SummaryAr, a.SummaryEn,
        JsonDefaults.Parse(a.BodyArJson) ?? new JsonArray(), JsonDefaults.Parse(a.BodyEnJson) ?? new JsonArray(),
        a.MetaTitleAr, a.MetaTitleEn, a.MetaDescriptionAr, a.MetaDescriptionEn,
        a.FeaturedImageId, a.FeaturedImage is null ? null : urls.Describe(a.FeaturedImage, a.FeaturedImage.AltEn.Length > 0 ? a.FeaturedImage.AltEn : a.FeaturedImage.AltAr),
        a.CategoryId, StatusOf(a.Status, a.PublishedAt, Now), a.PublishedAt, a.IsFeatured, a.Priority, a.CreatedAt, a.UpdatedAt, a.ConcurrencyStamp);

    internal static string StatusOf(NewsStatus status, DateTime? publishedAt, DateTime now) =>
        status == NewsStatus.Draft ? "draft" : publishedAt > now ? "scheduled" : "published";

    private static string Title(NewsArticle a) => a.TitleEn.Length > 0 ? a.TitleEn : a.TitleAr;

    // ------------------------------------------------------------------ categories

    public async Task<IReadOnlyList<CategoryDto>> CategoriesAsync(CancellationToken ct) =>
        await db.NewsCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Slug, c.NameAr, c.NameEn, c.SortOrder, c.Articles.Count))
            .ToListAsync(ct);

    public async Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryRequest r, CancellationToken ct)
    {
        var category = id is null ? new NewsCategory { Slug = "" } : await db.NewsCategories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw ApiError.NotFound();
        var errors = new FieldErrors();
        var nameAr = SafeText.Clean(r.NameAr);
        var nameEn = SafeText.Clean(r.NameEn);
        if (nameAr.Length == 0 && nameEn.Length == 0)
        {
            errors.Add("nameAr", "required");
        }

        if (nameAr.Length > 80)
        {
            errors.Add("nameAr", "too_long", 80);
        }

        if (nameEn.Length > 80)
        {
            errors.Add("nameEn", "too_long", 80);
        }

        var slug = (r.Slug ?? "").Trim().ToLowerInvariant();
        if (slug.Length == 0)
        {
            slug = SafeText.Slugify(nameEn);
            if (slug.Length < 3)
            {
                slug = $"category-{Guid.NewGuid().ToString("N")[..6]}";
            }
        }

        if (!SafeText.IsSlug(slug))
        {
            errors.Add("slug", "invalid_slug");
        }
        else if (await db.NewsCategories.AnyAsync(c => c.Slug == slug && c.Id != category.Id, ct))
        {
            errors.Add("slug", "slug_taken");
        }

        errors.ThrowIfAny();
        category.Slug = slug;
        category.NameAr = nameAr;
        category.NameEn = nameEn;
        category.SortOrder = r.SortOrder;
        if (id is null)
        {
            db.NewsCategories.Add(category);
        }

        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.CategoryChanged, "news_category", slug, $"{(id is null ? "Created" : "Edited")} category {slug}");
        await db.SaveChangesAsync(ct);
        return (await CategoriesAsync(ct)).First(c => c.Id == category.Id);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await db.NewsCategories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw ApiError.NotFound();

        // Articles keep existing without a category (the database also sets the key to NULL).
        await foreach (var article in db.News.Where(n => n.CategoryId == id).AsAsyncEnumerable().WithCancellation(ct))
        {
            article.CategoryId = null;
        }

        db.NewsCategories.Remove(category);
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.CategoryDeleted, "news_category", category.Slug, $"Deleted category {category.Slug}");
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ public

    /// <summary>Only published articles whose publication time has passed.</summary>
    public IQueryable<NewsArticle> Visible(DateTime now) =>
        db.News.AsNoTracking().Where(n => n.Status == NewsStatus.Published && n.PublishedAt != null && n.PublishedAt <= now);

    public async Task<PagedResult<JsonObject>> PublicListAsync(string lang, string? category, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize, defaultSize: 9, maxSize: 24);
        var now = Now;
        var query = Visible(now);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(n => n.Category != null && n.Category.Slug == category);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(n => n.Priority).ThenByDescending(n => n.PublishedAt).ThenByDescending(n => n.Id)
            .Skip((p - 1) * size).Take(size)
            .Include(n => n.FeaturedImage).Include(n => n.Category)
            .ToListAsync(ct);

        var media = rows.Where(r => r.FeaturedImage is not null).Select(r => r.FeaturedImage!).DistinctBy(m => m.Id).ToDictionary(m => m.Id);
        var localizer = new ContentLocalizer(lang, media, urls);
        var items = rows.Select(r => Card(r, localizer)).ToList();
        return new PagedResult<JsonObject>(items, p, size, total);
    }

    public async Task<JsonObject?> PublicGetAsync(string slug, string lang, CancellationToken ct)
    {
        var now = Now;
        var article = await Visible(now).Include(n => n.FeaturedImage).Include(n => n.Category).FirstOrDefaultAsync(n => n.Slug == slug, ct);
        return article is null ? null : await ArticleAsync(article, lang, now, includeRelated: true, ct);
    }

    /// <summary>The same shape as the public article, for any article (dashboard preview of drafts).</summary>
    public async Task<JsonObject> PreviewAsync(int id, string lang, CancellationToken ct)
    {
        var article = await db.News.AsNoTracking().Include(n => n.FeaturedImage).Include(n => n.Category).FirstOrDefaultAsync(n => n.Id == id, ct) ?? throw ApiError.NotFound();
        var result = await ArticleAsync(article, lang, Now, includeRelated: false, ct);
        result["preview"] = true;
        result["status"] = StatusOf(article.Status, article.PublishedAt, Now);
        return result;
    }

    public async Task<IReadOnlyList<JsonObject>> PublicCategoriesAsync(string lang, CancellationToken ct)
    {
        var now = Now;
        var cats = await db.NewsCategories.AsNoTracking()
            .Where(c => c.Articles.Any(n => n.Status == NewsStatus.Published && n.PublishedAt != null && n.PublishedAt <= now))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .ToListAsync(ct);
        var localizer = new ContentLocalizer(lang, new Dictionary<int, MediaAsset>(), urls);
        return cats.Select(c => new JsonObject { ["slug"] = c.Slug, ["name"] = localizer.Text(c.NameAr, c.NameEn) }).ToList();
    }

    private async Task<JsonObject> ArticleAsync(NewsArticle article, string lang, DateTime now, bool includeRelated, CancellationToken ct)
    {
        var (bodyJson, contentLang) = lang == "ar"
            ? (NewsBody.HasContent(article.BodyArJson) ? (article.BodyArJson, "ar") : (article.BodyEnJson, "en"))
            : (NewsBody.HasContent(article.BodyEnJson) ? (article.BodyEnJson, "en") : (article.BodyArJson, "ar"));

        var mediaIds = NewsBody.MediaIds(bodyJson).ToHashSet();
        var media = await db.Media.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        if (article.FeaturedImage is not null)
        {
            media[article.FeaturedImage.Id] = article.FeaturedImage;
        }

        var localizer = new ContentLocalizer(lang, media, urls);
        var body = new JsonArray();
        if (JsonDefaults.Parse(bodyJson) is JsonArray blocks)
        {
            foreach (var block in blocks)
            {
                if (block is not JsonObject obj)
                {
                    continue;
                }

                var copy = (JsonObject)obj.DeepClone();
                if (ContentValidator.ReadString(obj["type"]) == "image")
                {
                    var picture = localizer.Picture(ContentValidator.ReadInt(obj["mediaId"]));
                    if (picture is null)
                    {
                        continue;
                    }

                    copy.Remove("mediaId");
                    copy["image"] = picture;
                }

                body.Add(copy);
            }
        }

        var result = Card(article, localizer);
        // The article has its body as blocks; the card's flat text is not sent twice.
        result.Remove("text");
        // The article page shows only a summary that was written as its lede (never the body twice).
        result["summary"] = localizer.Text(article.SummaryAr, article.SummaryEn);
        result["contentLang"] = contentLang;
        result["body"] = body;
        result["updatedAt"] = article.UpdatedAt;
        result["metaTitle"] = localizer.Text(article.MetaTitleAr, article.MetaTitleEn) is { Length: > 0 } mt ? mt : result["title"]!.GetValue<string>();
        var summary = result["summary"]!.GetValue<string>();
        result["metaDescription"] = localizer.Text(article.MetaDescriptionAr, article.MetaDescriptionEn) is { Length: > 0 } md
            ? md
            : summary.Length > 0 ? summary : NewsBody.PlainText(bodyJson, 160);

        if (includeRelated)
        {
            var related = await Visible(now).Where(n => n.Id != article.Id)
                .OrderByDescending(n => n.PublishedAt).ThenByDescending(n => n.Id).Take(3)
                .Include(n => n.FeaturedImage).Include(n => n.Category).ToListAsync(ct);
            var relatedMedia = related.Where(r => r.FeaturedImage is not null).Select(r => r.FeaturedImage!).DistinctBy(m => m.Id).ToDictionary(m => m.Id);
            var relatedLocalizer = new ContentLocalizer(lang, relatedMedia, urls);
            result["related"] = new JsonArray(related.Select(r => (JsonNode)Card(r, relatedLocalizer)).ToArray());
        }

        return result;
    }

    private static JsonObject Card(NewsArticle a, ContentLocalizer l) => new()
    {
        ["slug"] = a.Slug,
        ["title"] = l.Text(a.TitleAr, a.TitleEn),
        ["summary"] = CardSummary(a, l),
        ["text"] = CardText(a, l),
        ["publishedAt"] = a.PublishedAt,
        ["featured"] = a.IsFeatured,
        ["image"] = l.Picture(a.FeaturedImageId),
        ["category"] = a.Category is null ? null : new JsonObject { ["slug"] = a.Category.Slug, ["name"] = l.Text(a.Category.NameAr, a.Category.NameEn) },
    };

    /// <summary>
    /// The summary written for the article, or, when there is none, the opening words of its text,
    /// so every news card has a line under its title.
    /// </summary>
    private static string CardSummary(NewsArticle a, ContentLocalizer l)
    {
        var summary = l.Text(a.SummaryAr, a.SummaryEn);
        if (summary.Length > 0)
        {
            return summary;
        }

        var (first, second) = l.Language == "ar" ? (a.BodyArJson, a.BodyEnJson) : (a.BodyEnJson, a.BodyArJson);
        return NewsBody.PlainText(NewsBody.HasContent(first) ? first : second, 180);
    }

    /// <summary>
    /// Everything a news card on the website shows under its title: the summary written for the
    /// article, then the article's own words (<see cref="NewsBody.CardText"/>). Long text is folded
    /// on the card and opens in place, so a visitor reads the whole story without leaving the list.
    /// </summary>
    private static string CardText(NewsArticle a, ContentLocalizer l)
    {
        var (first, second) = l.Language == "ar" ? (a.BodyArJson, a.BodyEnJson) : (a.BodyEnJson, a.BodyArJson);
        var body = NewsBody.CardText(NewsBody.HasContent(first) ? first : second);
        var summary = l.Text(a.SummaryAr, a.SummaryEn).Trim();
        if (summary.Length == 0)
        {
            return body;
        }

        return body.Length == 0 ? summary : summary + "\n\n" + body;
    }
}
