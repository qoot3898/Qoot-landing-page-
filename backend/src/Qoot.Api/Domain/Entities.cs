using Microsoft.AspNetCore.Identity;

namespace Qoot.Api.Domain;

/// <summary>An administrator account. There is no public registration: accounts are created by an admin or the CLI.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string PreferredLanguage { get; set; } = "ar";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { }
}

public static class Roles
{
    /// <summary>Full access, including users, settings and the audit log.</summary>
    public const string Admin = "Admin";

    /// <summary>Content only: sections, news, media and Contact us messages.</summary>
    public const string Editor = "Editor";

    public static readonly string[] All = [Admin, Editor];
}

public static class Policies
{
    public const string ContentEditor = nameof(ContentEditor);
    public const string Administrator = nameof(Administrator);
}

/// <summary>
/// One section of the public page (hero, supplies, FAQ, ...). Content is stored as JSON shaped by the
/// section's schema (see <c>Content/SectionSchemas.cs</c>), once as the working draft and once as the
/// published version that visitors see.
/// </summary>
public sealed class Section
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string DraftJson { get; set; } = "{}";
    public string? PublishedJson { get; set; }
    public bool HasUnpublishedChanges { get; set; }
    public DateTime DraftUpdatedAt { get; set; }
    public Guid? DraftUpdatedById { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? PublishedById { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

/// <summary>Global settings (contact details, SEO, languages, opening hours). A single row, JSON shaped by its schema.</summary>
public sealed class SiteSettings
{
    public int Id { get; set; }
    public string Json { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

/// <summary>Bumped on every change that affects what visitors see, so caches on every server instance can be invalidated.</summary>
public sealed class ContentState
{
    public int Id { get; set; }
    public long Version { get; set; }
    public DateTime ChangedAt { get; set; }
}

public enum NewsStatus
{
    Draft = 0,
    Published = 1,
}

public sealed class NewsArticle
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public string TitleAr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string SummaryAr { get; set; } = "";
    public string SummaryEn { get; set; } = "";

    /// <summary>Structured body blocks (paragraphs, headings, lists, quotes, images) as JSON. Never HTML.</summary>
    public string BodyArJson { get; set; } = "[]";

    public string BodyEnJson { get; set; } = "[]";
    public string MetaTitleAr { get; set; } = "";
    public string MetaTitleEn { get; set; } = "";
    public string MetaDescriptionAr { get; set; } = "";
    public string MetaDescriptionEn { get; set; } = "";
    public int? FeaturedImageId { get; set; }
    public MediaAsset? FeaturedImage { get; set; }
    public int? CategoryId { get; set; }
    public NewsCategory? Category { get; set; }
    public NewsStatus Status { get; set; }

    /// <summary>When the article goes live. A date in the future schedules a published article.</summary>
    public DateTime? PublishedAt { get; set; }

    public bool IsFeatured { get; set; }

    /// <summary>Higher numbers are listed first; equal priority falls back to newest first.</summary>
    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

public sealed class NewsCategory
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public int SortOrder { get; set; }
    public List<NewsArticle> Articles { get; set; } = [];
}

public enum MediaKind
{
    /// <summary>Uploaded through the dashboard and stored by the API.</summary>
    Upload = 0,

    /// <summary>One of Qoot's brand files shipped with the website (cannot be deleted, can be described and replaced in content).</summary>
    Bundled = 1,
}

public sealed class MediaAsset
{
    public int Id { get; set; }
    public MediaKind Kind { get; set; }

    /// <summary>Uploads: path relative to the storage root (yyyy/MM/guid.ext). Bundled: site path (/assets/...).</summary>
    public required string StoragePath { get; set; }

    public string FileName { get; set; } = "";
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string AltAr { get; set; } = "";
    public string AltEn { get; set; } = "";

    /// <summary>Bundled responsive sources as JSON: [{ "type": "image/avif", "srcset": "..." }].</summary>
    public string? SourcesJson { get; set; }

    public string? Sha256 { get; set; }

    /// <summary>Set on brand files created by the seeder, so they are recognised even after being replaced.</summary>
    public string? SeedKey { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum SubmissionStatus
{
    New = 0,
    InProgress = 1,
    Done = 2,
    Archived = 3,
}

/// <summary>A message sent from the Contact us form on the public site.</summary>
public sealed class ContactSubmission
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>The number to call back on (required on the form).</summary>
    public string Phone { get; set; } = "";

    /// <summary>Optional.</summary>
    public string Email { get; set; } = "";

    public string Message { get; set; } = "";
    public string Language { get; set; } = "ar";
    public SubmissionStatus Status { get; set; }
    public string AdminNotes { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = "";
    public required string Action { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? IpAddress { get; set; }
}
