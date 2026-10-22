using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qoot.Api.Content;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;
using Qoot.Api.Media;

namespace Qoot.Api.Services;

public sealed record MediaDto(
    int Id,
    string Kind,
    string Url,
    string ContentType,
    string FileName,
    long SizeBytes,
    int Width,
    int Height,
    string AltAr,
    string AltEn,
    bool IsDrawing,
    JsonObject Picture,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record MediaUsageDto(string Area, string Key, string LabelAr, string LabelEn);

public sealed record UpdateMediaRequest(string? AltAr, string? AltEn, string? FileName);

public sealed record MediaQuery(string? Search, string? Kind, string? Type, int? Page, int? PageSize);

public sealed class MediaService(
    QootDbContext db,
    IMediaStorage storage,
    IOptions<MediaOptions> options,
    MediaUrls urls,
    ContentVersion version,
    AuditService audit,
    ICurrentUser user,
    TimeProvider clock)
{
    private readonly MediaOptions _options = options.Value;

    public async Task<PagedResult<MediaDto>> ListAsync(MediaQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(q.Page, q.PageSize, defaultSize: 24, maxSize: 96);
        var query = db.Media.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(m => EF.Functions.Like(m.FileName, term) || EF.Functions.Like(m.AltAr, term) || EF.Functions.Like(m.AltEn, term));
        }

        query = q.Kind switch
        {
            "upload" => query.Where(m => m.Kind == MediaKind.Upload),
            "bundled" => query.Where(m => m.Kind == MediaKind.Bundled),
            _ => query,
        };

        query = q.Type switch
        {
            "photo" => query.Where(m => m.ContentType != "image/svg+xml"),
            "drawing" => query.Where(m => m.ContentType == "image/svg+xml"),
            _ => query,
        };

        var result = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).ToPageAsync(page, size, ct);
        return new PagedResult<MediaDto>(result.Items.Select(ToDto).ToList(), result.Page, result.PageSize, result.Total);
    }

    public async Task<MediaDto> GetAsync(int id, CancellationToken ct) =>
        ToDto(await db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw ApiError.NotFound());

    public async Task<IReadOnlyList<MediaDto>> UploadAsync(IReadOnlyList<IFormFile> files, string? altAr, string? altEn, CancellationToken ct)
    {
        if (files.Count == 0)
        {
            throw ApiError.BadRequest("file_missing");
        }

        if (files.Count > _options.MaxFilesPerUpload)
        {
            throw ApiError.BadRequest("too_many_files", _options.MaxFilesPerUpload);
        }

        var prepared = new List<(IFormFile File, byte[] Bytes, ImageInfo Info)>();
        var errors = new FieldErrors();
        for (var i = 0; i < files.Count; i++)
        {
            var (bytes, info) = await ReadImageAsync(files[i], $"files[{i}]", errors, ct);
            if (bytes is not null && info is not null)
            {
                prepared.Add((files[i], bytes, info));
            }
        }

        errors.ThrowIfAny();

        var now = clock.GetUtcNow().UtcDateTime;
        var created = new List<MediaAsset>();
        foreach (var (file, bytes, info) in prepared)
        {
            var path = await storage.SaveAsync(bytes, info.Extension, ct);
            var asset = new MediaAsset
            {
                Kind = MediaKind.Upload,
                StoragePath = path,
                FileName = CleanFileName(file.FileName, info.Extension),
                ContentType = info.ContentType,
                SizeBytes = bytes.LongLength,
                Width = info.Width,
                Height = info.Height,
                AltAr = Limit(SafeText.Clean(altAr), 300),
                AltEn = Limit(SafeText.Clean(altEn), 300),
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = user.Id,
            };
            db.Media.Add(asset);
            created.Add(asset);
            audit.Record(AuditActions.MediaUploaded, "media", path, $"Uploaded {asset.FileName} ({info.Width}×{info.Height})");
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var asset in created)
            {
                storage.Delete(asset.StoragePath);
            }

            throw;
        }

        return created.Select(ToDto).ToList();
    }

    /// <summary>
    /// Puts a new file behind an existing media item. Every section and article that uses the item shows
    /// the new picture at once, so nothing on the site breaks. Brand files become regular uploads.
    /// </summary>
    public async Task<MediaDto> ReplaceAsync(int id, IFormFile? file, CancellationToken ct)
    {
        var asset = await db.Media.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw ApiError.NotFound();
        if (file is null)
        {
            throw ApiError.BadRequest("file_missing");
        }

        var errors = new FieldErrors();
        var (bytes, info) = await ReadImageAsync(file, "file", errors, ct);
        errors.ThrowIfAny();

        var oldPath = asset.Kind == MediaKind.Upload ? asset.StoragePath : null;
        var newPath = await storage.SaveAsync(bytes!, info!.Extension, ct);
        asset.Kind = MediaKind.Upload;
        asset.StoragePath = newPath;
        asset.SourcesJson = null;
        asset.FileName = CleanFileName(file.FileName, info.Extension);
        asset.ContentType = info.ContentType;
        asset.SizeBytes = bytes!.LongLength;
        asset.Width = info.Width;
        asset.Height = info.Height;
        asset.Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        asset.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.MediaReplaced, "media", id.ToString(CultureInfo.InvariantCulture), $"Replaced the file of media #{id} with {asset.FileName}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(newPath);
            throw;
        }

        if (oldPath is not null)
        {
            storage.Delete(oldPath);
        }

        return ToDto(asset);
    }

    public async Task<MediaDto> UpdateAsync(int id, UpdateMediaRequest r, CancellationToken ct)
    {
        var asset = await db.Media.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw ApiError.NotFound();
        var errors = new FieldErrors();
        var altAr = SafeText.Clean(r.AltAr);
        var altEn = SafeText.Clean(r.AltEn);
        if (altAr.Length > 300)
        {
            errors.Add("altAr", "too_long", 300);
        }

        if (altEn.Length > 300)
        {
            errors.Add("altEn", "too_long", 300);
        }

        var fileName = SafeText.Clean(r.FileName);
        if (fileName.Length > 200)
        {
            errors.Add("fileName", "too_long", 200);
        }

        errors.ThrowIfAny();
        asset.AltAr = altAr;
        asset.AltEn = altEn;
        if (fileName.Length > 0)
        {
            asset.FileName = fileName;
        }

        asset.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await version.MarkChangedAsync(ct);
        audit.Record(AuditActions.MediaUpdated, "media", id.ToString(CultureInfo.InvariantCulture), $"Edited the description of {asset.FileName}");
        await db.SaveChangesAsync(ct);
        return ToDto(asset);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var asset = await db.Media.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw ApiError.NotFound();
        if (asset.Kind == MediaKind.Bundled)
        {
            throw ApiError.Conflict("media_bundled");
        }

        var usages = await UsageAsync(id, ct);
        if (usages.Count > 0)
        {
            var list = string.Join(Messages.IsArabic ? "، " : ", ", usages.Select(u => Messages.IsArabic ? u.LabelAr : u.LabelEn).Distinct());
            throw ApiError.Conflict("media_in_use", usages, list);
        }

        db.Media.Remove(asset);
        audit.Record(AuditActions.MediaDeleted, "media", id.ToString(CultureInfo.InvariantCulture), $"Deleted {asset.FileName}");
        await db.SaveChangesAsync(ct);
        storage.Delete(asset.StoragePath);
    }

    /// <summary>Everywhere a media item is used: section drafts and published versions, settings and articles.</summary>
    public async Task<IReadOnlyList<MediaUsageDto>> UsageAsync(int id, CancellationToken ct)
    {
        var usages = new List<MediaUsageDto>();
        var sections = await db.Sections.AsNoTracking().ToListAsync(ct);
        foreach (var section in sections)
        {
            if (SectionSchemas.Find(section.Key) is not { } schema)
            {
                continue;
            }

            var ids = new List<int>();
            MediaRefCollector.Collect(schema.Fields, JsonDefaults.Parse(section.DraftJson), ids);
            MediaRefCollector.Collect(schema.Fields, JsonDefaults.Parse(section.PublishedJson), ids);
            if (ids.Contains(id))
            {
                usages.Add(new MediaUsageDto("section", section.Key, schema.Name.Ar, schema.Name.En));
            }
        }

        var settingsJson = await db.SiteSettings.AsNoTracking().Select(s => s.Json).FirstOrDefaultAsync(ct);
        var settingIds = new List<int>();
        MediaRefCollector.Collect(SettingsSchema.Schema.Fields, JsonDefaults.Parse(settingsJson), settingIds);
        if (settingIds.Contains(id))
        {
            usages.Add(new MediaUsageDto("settings", "settings", SettingsSchema.Schema.Name.Ar, SettingsSchema.Schema.Name.En));
        }

        var idText = id.ToString(CultureInfo.InvariantCulture);
        var pattern = $"%\"mediaId\":{idText}%";
        var articles = await db.News.AsNoTracking()
            .Where(n => n.FeaturedImageId == id || EF.Functions.Like(n.BodyArJson, pattern) || EF.Functions.Like(n.BodyEnJson, pattern))
            .Select(n => new { n.Id, n.TitleAr, n.TitleEn, n.FeaturedImageId, n.BodyArJson, n.BodyEnJson })
            .ToListAsync(ct);
        foreach (var a in articles)
        {
            if (a.FeaturedImageId == id || NewsBody.MediaIds(a.BodyArJson).Contains(id) || NewsBody.MediaIds(a.BodyEnJson).Contains(id))
            {
                usages.Add(new MediaUsageDto("news", a.Id.ToString(CultureInfo.InvariantCulture), $"خبر: {(a.TitleAr.Length > 0 ? a.TitleAr : a.TitleEn)}", $"Article: {(a.TitleEn.Length > 0 ? a.TitleEn : a.TitleAr)}"));
            }
        }

        return usages;
    }

    private async Task<(byte[]? Bytes, ImageInfo? Info)> ReadImageAsync(IFormFile file, string field, FieldErrors errors, CancellationToken ct)
    {
        if (file.Length <= 0)
        {
            errors.Add(field, "file_missing");
            return (null, null);
        }

        if (file.Length > _options.MaxUploadBytes)
        {
            errors.Add(field, "file_too_large", _options.MaxUploadBytes / (1024 * 1024));
            return (null, null);
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var info = ImageInspector.Inspect(bytes);
        if (info is null)
        {
            errors.Add(field, file.FileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || bytes.Length < 32 ? "file_type" : "file_corrupt");
            return (null, null);
        }

        if (info.Width <= 0 || info.Height <= 0 || info.Width > _options.MaxDimension || info.Height > _options.MaxDimension)
        {
            errors.Add(field, "file_dimensions", _options.MaxDimension);
            return (null, null);
        }

        return (bytes, info);
    }

    private static string CleanFileName(string original, string extension)
    {
        var name = SafeText.Clean(Path.GetFileNameWithoutExtension(original ?? ""));
        name = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
        if (name.Length == 0)
        {
            name = "image";
        }

        return Limit(name, 180) + extension;
    }

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];

    private MediaDto ToDto(MediaAsset m) => new(
        m.Id, m.Kind == MediaKind.Bundled ? "bundled" : "upload", urls.Url(m), m.ContentType, m.FileName, m.SizeBytes, m.Width, m.Height,
        m.AltAr, m.AltEn, m.ContentType == "image/svg+xml", urls.Describe(m, m.AltEn.Length > 0 ? m.AltEn : m.AltAr), m.CreatedAt, m.UpdatedAt);
}
