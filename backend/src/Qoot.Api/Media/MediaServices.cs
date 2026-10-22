using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Qoot.Api.Domain;

namespace Qoot.Api.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Folder where uploads are stored. Relative paths are resolved from the app's content root. Mount persistent storage here.</summary>
    public string RootPath { get; set; } = "App_Data/media";

    /// <summary>
    /// Origin that serves /media/... to visitors. Leave empty when the website and the API share an
    /// origin; set it (for example https://api.qoot.com) when they do not.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";

    public long MaxUploadBytes { get; set; } = 8 * 1024 * 1024;
    public int MaxDimension { get; set; } = 12000;
    public int MaxFilesPerUpload { get; set; } = 10;
}

/// <summary>Builds the picture description sent to browsers.</summary>
public sealed class MediaUrls(IOptions<MediaOptions> options)
{
    private readonly string _base = options.Value.PublicBaseUrl.TrimEnd('/');

    public string Url(MediaAsset asset) => asset.Kind == MediaKind.Bundled ? asset.StoragePath : $"{_base}/media/{asset.StoragePath}";

    public JsonObject Describe(MediaAsset asset, string alt)
    {
        var sources = new JsonArray();
        if (!string.IsNullOrEmpty(asset.SourcesJson))
        {
            try
            {
                foreach (var source in JsonSerializer.Deserialize<List<MediaSource>>(asset.SourcesJson, JsonSerializerOptions.Web) ?? [])
                {
                    sources.Add(new JsonObject { ["type"] = source.Type, ["srcset"] = source.Srcset });
                }
            }
            catch (JsonException)
            {
                // A malformed source list only loses the responsive variants; the main file still works.
            }
        }

        return new JsonObject
        {
            ["id"] = asset.Id,
            ["url"] = Url(asset),
            ["type"] = asset.ContentType,
            ["width"] = asset.Width,
            ["height"] = asset.Height,
            ["alt"] = alt,
            ["sources"] = sources,
        };
    }
}

public sealed record MediaSource(string Type, string Srcset);

public sealed record ImageInfo(string ContentType, string Extension, int Width, int Height);

/// <summary>
/// Identifies an uploaded image from its bytes (never from the file name or the browser's claimed type)
/// and reads its pixel size. Anything that is not a recognisable JPEG, PNG, GIF, WebP or AVIF is refused.
/// SVG is not accepted from uploads because it can carry script.
/// </summary>
public static class ImageInspector
{
    public static ImageInfo? Inspect(ReadOnlySpan<byte> data)
    {
        if (data.Length < 32)
        {
            return null;
        }

        if (data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) && data[12..16].SequenceEqual("IHDR"u8))
        {
            return new ImageInfo("image/png", ".png", (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..20]), (int)BinaryPrimitives.ReadUInt32BigEndian(data[20..24]));
        }

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return Jpeg(data);
        }

        if (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8))
        {
            return new ImageInfo("image/gif", ".gif", BinaryPrimitives.ReadUInt16LittleEndian(data[6..8]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..10]));
        }

        if (data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return WebP(data);
        }

        if (data[4..8].SequenceEqual("ftyp"u8))
        {
            return Avif(data);
        }

        return null;
    }

    private static ImageInfo? Jpeg(ReadOnlySpan<byte> data)
    {
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                return null;
            }

            var marker = data[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }

            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7))
            {
                i += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 2, 2));
            if (marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                var height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 7, 2));
                return width > 0 && height > 0 ? new ImageInfo("image/jpeg", ".jpg", width, height) : null;
            }

            if (length < 2)
            {
                return null;
            }

            i += 2 + length;
        }

        return null;
    }

    private static ImageInfo? WebP(ReadOnlySpan<byte> data)
    {
        var chunk = Encoding.ASCII.GetString(data[12..16]);
        switch (chunk)
        {
            case "VP8 " when data.Length >= 30 && data[23] == 0x9D && data[24] == 0x01 && data[25] == 0x2A:
                return new ImageInfo("image/webp", ".webp", BinaryPrimitives.ReadUInt16LittleEndian(data[26..28]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(data[28..30]) & 0x3FFF);
            case "VP8L" when data.Length >= 25 && data[20] == 0x2F:
            {
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..25]);
                return new ImageInfo("image/webp", ".webp", (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
            }

            case "VP8X" when data.Length >= 30:
                return new ImageInfo("image/webp", ".webp", (data[24] | (data[25] << 8) | (data[26] << 16)) + 1, (data[27] | (data[28] << 8) | (data[29] << 16)) + 1);
            default:
                return null;
        }
    }

    private static ImageInfo? Avif(ReadOnlySpan<byte> data)
    {
        var ftypSize = (int)BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
        if (ftypSize < 16 || ftypSize > data.Length)
        {
            return null;
        }

        var brands = data[8..ftypSize];
        var isAvif = false;
        for (var b = 0; b + 4 <= brands.Length; b += 4)
        {
            if (brands.Slice(b, 4).SequenceEqual("avif"u8) || brands.Slice(b, 4).SequenceEqual("avis"u8))
            {
                isAvif = true;
                break;
            }
        }

        if (!isAvif)
        {
            return null;
        }

        var window = data[..Math.Min(data.Length, 64 * 1024)];
        var at = window.IndexOf("ispe"u8);
        if (at < 4 || at + 16 > window.Length)
        {
            return null;
        }

        var width = (int)BinaryPrimitives.ReadUInt32BigEndian(window.Slice(at + 8, 4));
        var height = (int)BinaryPrimitives.ReadUInt32BigEndian(window.Slice(at + 12, 4));
        return width > 0 && height > 0 ? new ImageInfo("image/avif", ".avif", width, height) : null;
    }
}

public interface IMediaStorage
{
    string RootPath { get; }

    Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct);

    void Delete(string relativePath);
}

/// <summary>Stores uploads on disk under generated names (year/month/guid.ext); user-supplied names never touch the file system.</summary>
public sealed class LocalDiskMediaStorage : IMediaStorage
{
    private readonly ILogger<LocalDiskMediaStorage> _logger;

    public LocalDiskMediaStorage(IOptions<MediaOptions> options, IWebHostEnvironment env, ILogger<LocalDiskMediaStorage> logger)
    {
        _logger = logger;
        var root = options.Value.RootPath;
        RootPath = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct)
    {
        if (extension is not (".jpg" or ".png" or ".gif" or ".webp" or ".avif"))
        {
            throw new ArgumentException("Unsupported extension.", nameof(extension));
        }

        var now = DateTime.UtcNow;
        var relative = $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, ct);
        return relative;
    }

    public void Delete(string relativePath)
    {
        try
        {
            var full = Resolve(relativePath);
            if (File.Exists(full))
            {
                File.Delete(full);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not delete media file {Path}", relativePath);
        }
    }

    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(RootPath, relativePath));
        if (!full.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Path escapes the media root.", nameof(relativePath));
        }

        return full;
    }
}
