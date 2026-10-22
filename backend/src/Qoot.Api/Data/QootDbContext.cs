using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Qoot.Api.Domain;

namespace Qoot.Api.Data;

public sealed class QootDbContext(DbContextOptions<QootDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<ContentState> ContentStates => Set<ContentState>();
    public DbSet<NewsArticle> News => Set<NewsArticle>();
    public DbSet<NewsCategory> NewsCategories => Set<NewsCategory>();
    public DbSet<MediaAsset> Media => Set<MediaAsset>();
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every timestamp is stored in UTC and read back as UTC, so JSON always carries a "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(120);
            e.Property(x => x.PreferredLanguage).HasMaxLength(5);
        });

        builder.Entity<Section>(e =>
        {
            e.ToTable("Sections");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(40);
            e.Property(x => x.DraftJson).IsRequired();
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        });

        builder.Entity<SiteSettings>(e =>
        {
            e.ToTable("SiteSettings");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        });

        builder.Entity<ContentState>(e =>
        {
            e.ToTable("ContentState");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        builder.Entity<NewsCategory>(e =>
        {
            e.ToTable("NewsCategories");
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(80);
            e.Property(x => x.NameAr).HasMaxLength(80);
            e.Property(x => x.NameEn).HasMaxLength(80);
        });

        builder.Entity<NewsArticle>(e =>
        {
            e.ToTable("NewsArticles");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Status, x.PublishedAt });
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.TitleAr).HasMaxLength(200);
            e.Property(x => x.TitleEn).HasMaxLength(200);
            e.Property(x => x.SummaryAr).HasMaxLength(600);
            e.Property(x => x.SummaryEn).HasMaxLength(600);
            e.Property(x => x.MetaTitleAr).HasMaxLength(200);
            e.Property(x => x.MetaTitleEn).HasMaxLength(200);
            e.Property(x => x.MetaDescriptionAr).HasMaxLength(320);
            e.Property(x => x.MetaDescriptionEn).HasMaxLength(320);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            e.HasOne(x => x.FeaturedImage).WithMany().HasForeignKey(x => x.FeaturedImageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Articles).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MediaAsset>(e =>
        {
            e.ToTable("MediaAssets");
            e.Property(x => x.StoragePath).HasMaxLength(400);
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(80);
            e.Property(x => x.AltAr).HasMaxLength(300);
            e.Property(x => x.AltEn).HasMaxLength(300);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SeedKey).HasMaxLength(60);
            e.HasIndex(x => x.StoragePath).IsUnique();
            e.HasIndex(x => x.SeedKey).IsUnique();
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<ContactSubmission>(e =>
        {
            e.ToTable("ContactSubmissions");
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.Language).HasMaxLength(5);
            e.Property(x => x.AdminNotes).HasMaxLength(2000);
            e.Property(x => x.IpHash).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(300);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        builder.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditLog");
            e.Property(x => x.Action).HasMaxLength(60);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.EntityType).HasMaxLength(40);
            e.Property(x => x.EntityId).HasMaxLength(80);
            e.Property(x => x.Summary).HasMaxLength(500);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.At);
        });
    }
}

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v.HasValue ? (v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : v,
    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
