# Database

SQL Server (2019+, or Azure SQL) through EF Core 10. One migration, `InitialCreate`
(`backend/src/Qoot.Api/Data/Migrations`), creates everything; [`database/qoot-schema.sql`](database/qoot-schema.sql)
is the same schema as an idempotent script.

## Tables

| Table | What it holds | Key columns |
| --- | --- | --- |
| `Sections` | One row per home-page section | `Key` (unique), `SortOrder`, `IsVisible`, `DraftJson`, `PublishedJson`, `HasUnpublishedChanges`, `DraftUpdatedAt/ById`, `PublishedAt/ById`, `ConcurrencyStamp` |
| `SiteSettings` | One row: contact details, SEO, hours, languages, notifications | `Json`, `UpdatedAt/ById`, `ConcurrencyStamp` |
| `ContentState` | One row: a version number bumped on every public change (cache key) | `Version`, `ChangedAt` |
| `NewsArticles` | Latest news, both languages | `Slug` (unique), `TitleAr/En`, `SummaryAr/En`, `BodyArJson/BodyEnJson` (structured blocks), `MetaTitle*/MetaDescription*`, `FeaturedImageId` → `MediaAssets`, `CategoryId` → `NewsCategories`, `Status` (`draft`/`published`), `PublishedAt` (a future date = scheduled), `IsFeatured`, `Priority`, `ConcurrencyStamp` |
| `NewsCategories` | Optional categories | `Slug` (unique), `NameAr`, `NameEn`, `SortOrder` |
| `MediaAssets` | Pictures: uploads and the bundled brand files | `Kind` (`upload`/`bundled`), `StoragePath` (unique, generated), `FileName`, `ContentType`, `SizeBytes`, `Width`, `Height`, `AltAr`, `AltEn`, `SourcesJson` (AVIF/WebP sizes of brand photos), `Sha256`, `SeedKey` |
| `ContactSubmissions` | Messages from the Contact us form | `Name`, `Phone`, `Email` (optional), `Message`, `Language`, `Status` (`new`/`in_progress`/`done`/`archived`), `IsRead`, `AdminNotes`, `CreatedAt`, `IpHash` (SHA-256, not the address), `UserAgent` |
| `AuditLog` | Sign-ins and every change | `At`, `UserId`, `UserEmail`, `Action` (e.g. `news.published`), `EntityType`, `EntityId`, `Summary`, `IpAddress` |
| `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, … | ASP.NET Core Identity (hashed passwords, lockout, security stamps) plus `DisplayName`, `IsActive`, `PreferredLanguage`, `LastLoginAt` | roles: `Admin`, `Editor` |
| `__EFMigrationsHistory` | Applied migrations | |

Section and settings content is JSON validated against code-defined schemas
(`Content/SectionSchemas.cs`, `Content/SettingsSchema.cs`): each editable text is stored as
`{ "ar": "…", "en": "…" }`, pictures as media ids, repeated items as arrays with a stable `id` and a
`hidden` flag. A schema change needs no migration; unknown keys are dropped on save.

Indexes: `Sections.Key`, `NewsArticles.Slug`, `NewsArticles(Status, PublishedAt)`,
`NewsCategories.Slug`, `MediaAssets.StoragePath`, `MediaAssets.SeedKey`,
`ContactSubmissions(Status, CreatedAt)`, `AuditLog.At`, plus Identity's.

## Setting it up

1. Create an empty database and a login for the app (least privilege: `db_datareader`,
   `db_datawriter`, plus `db_ddladmin` only for the account that runs migrations).
2. Set `ConnectionStrings__Qoot`.
3. Apply the schema and the seed content, either:
   * `dotnet Qoot.Api.dll migrate` (from the published folder; safe to repeat), or
   * a DBA runs `docs/database/qoot-schema.sql`, then `dotnet Qoot.Api.dll seed`.
4. Create the first administrator: `dotnet Qoot.Api.dll create-admin --email …`.

The seed (`Data/Seed/*.json`) is the original website's Arabic and English content, extracted from
its HTML, and the brand pictures it used. It only fills what is missing, so it never overwrites
edits made in the dashboard.

## Backups

Back up the database and the `Media:RootPath` folder (`App_Data/media`) together: rows in
`MediaAssets` point at files there. Keep `App_Data/keys` too (losing it only signs everyone out).

## Changing the model

```bash
cd backend
dotnet run --project tools/Qoot.MigrationsTool -- add <Name>         # new migration in Data/Migrations
dotnet run --project tools/Qoot.MigrationsTool -- check              # fails if the model and migrations differ
dotnet run --project tools/Qoot.MigrationsTool -- script ../docs/database/qoot-schema.sql
```

## Migrations

| Migration | What it does |
| --- | --- |
| `20261021103826_InitialCreate` | Every table above. |
| `20261022080000_ContactUs` | Quote requests become Contact us messages: adds `Email`, renames `Need` → `Message` and `Contact` → `Phone` (40 characters), and removes `People`, `Frequency`, `Organisation`, `Location`. Messages already received keep everything: an email typed in the old contact box moves to `Email`, and the old quote details are added to the end of the message text. |

The website section that held the quote form (`quote`) is replaced by `contact` when the API starts
(the seeder, `DataSeeder.ReplaceQuoteSectionAsync`): same position and visibility, links to `#quote`
point to `#contact`, and texts still exactly as first seeded that talked about quotes get the Contact
us wording. Texts an editor had changed are left as they are.
