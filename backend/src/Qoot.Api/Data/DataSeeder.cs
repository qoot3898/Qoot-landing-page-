using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Content;
using Qoot.Api.Domain;
using Qoot.Api.Media;
using Qoot.Api.Services;

namespace Qoot.Api.Data;

public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>
    /// Creates the first administrator on start-up when no account exists yet. Meant for container
    /// deployments; prefer the create-admin command. Remove the values after the first start.
    /// </summary>
    public string AdminEmail { get; set; } = "";

    public string AdminPassword { get; set; } = "";
    public string AdminName { get; set; } = "";

    /// <summary>
    /// Development only: on every start, make sure the AdminEmail account exists, is an active
    /// administrator, is unlocked and signs in with AdminPassword, even when other accounts exist.
    /// Ignored (with a warning) in every other environment.
    /// </summary>
    public bool SyncInDevelopment { get; set; }
}

/// <summary>
/// Fills an empty database with the content of the current Qoot page (both languages), the brand
/// pictures that ship with the website and the default settings. Safe to run on every start: it only
/// adds what is missing and never overwrites content an editor has changed.
/// </summary>
public sealed class DataSeeder(
    QootDbContext db,
    RoleManager<AppRole> roles,
    UserManager<AppUser> users,
    TimeProvider clock,
    ILogger<DataSeeder> logger)
{
    private sealed record SeedMedia(
        string Key, string Path, string Type, int Width, int Height, string AltAr, string AltEn, string FileName, List<MediaSource> Sources);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new AppRole(role));
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var mediaKeys = await SeedMediaAsync(now, ct);

        if (!await db.SiteSettings.AnyAsync(ct))
        {
            var settings = Resolve(ReadSeed("settings.json"), mediaKeys);
            var ctx = new ContentValidationContext();
            var data = ContentValidator.Validate(SettingsSchema.Schema.Fields, settings, ctx);
            EnsureValid(ctx, "settings");
            db.SiteSettings.Add(new SiteSettings { Id = SettingsService.RowId, Json = data.ToJsonString(), UpdatedAt = now });
        }

        var sectionsSeed = ReadSeed("sections.json") as JsonObject ?? new JsonObject();
        await ReplaceQuoteSectionAsync(sectionsSeed, mediaKeys, now, ct);
        var existing = await db.Sections.Select(s => s.Key).ToListAsync(ct);
        var order = 0;
        foreach (var key in SectionSchemas.DefaultOrder)
        {
            order += 10;
            if (existing.Contains(key))
            {
                continue;
            }

            var schema = SectionSchemas.Find(key)!;
            var ctx = new ContentValidationContext();
            var data = ContentValidator.Validate(schema.Fields, Resolve(sectionsSeed[key]?.DeepClone(), mediaKeys), ctx);
            EnsureValid(ctx, key);
            var json = data.ToJsonString();
            db.Sections.Add(new Section
            {
                Key = key,
                SortOrder = key == SectionSchemas.Header ? 0 : key == SectionSchemas.Footer ? 10_000 : order,
                IsVisible = true,
                DraftJson = json,
                PublishedJson = json,
                DraftUpdatedAt = now,
                PublishedAt = now,
            });
            logger.LogInformation("Seeded section {Key}", key);
        }

        if (!await db.ContentStates.AnyAsync(ct))
        {
            db.ContentStates.Add(new ContentState { Id = ContentVersion.RowId, Version = 1, ChangedAt = now });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The key of the section that "Contact us" replaced (22 October 2026).</summary>
    public const string RetiredQuoteKey = "quote";

    /// <summary>
    /// Texts as they were first seeded that talked about quote requests, and their Contact us
    /// wording. Only a value that is still exactly the old text is replaced: anything an editor
    /// wrote stays as it is.
    /// </summary>
    private static readonly Dictionary<string, string> RetiredTexts = new(StringComparer.Ordinal)
    {
        ["#quote"] = "#contact",
        ["اطلب عرض سعر"] = "تواصل معنا",
        ["Request a quote"] = "Contact us",
        ["اذكره في طلب عرض السعر"] = "اذكره في رسالتك إلينا",
        ["Mention it in your quote request"] = "Mention it when you contact us",
        ["نعم، ويعتمد على مسافة التوصيل. نحدّده لك بوضوح في عرض السعر."] = "نعم، ويعتمد على مسافة التوصيل. تواصل معنا ونخبرك به بوضوح.",
        ["Yes. It depends on the delivery distance, and we state it clearly in your quote."] = "Yes. It depends on the delivery distance; contact us and we’ll tell you exactly.",
        ["تُسعَّر الإعاشة بالفرد في اليوم، والتوريد حسب قائمة الأسعار. نرسل عرض سعر مكتوبًا خلال يومي عمل من طلبك."] = "تُسعَّر الإعاشة بالفرد في اليوم، والتوريد حسب قائمة الأسعار. تواصل معنا ونرسل لك الأسعار مكتوبة خلال يومي عمل.",
        ["Catering is priced per person per day, and supplies by price list. We send a written quote within two working days of your request."] = "Catering is priced per person per day, and supplies by price list. Contact us and we send written prices within two working days.",
    };

    /// <summary>
    /// "Request a quote" became "Contact us". On a database created before that change, the old
    /// section is replaced by the new one in the same place (same position, same visibility), links
    /// to #quote now point to #contact, and the seeded texts about quotes get their new wording.
    /// Runs once: afterwards there is no "quote" section left to replace.
    /// </summary>
    private async Task ReplaceQuoteSectionAsync(JsonObject sectionsSeed, IReadOnlyDictionary<string, int> mediaKeys, DateTime now, CancellationToken ct)
    {
        var quote = await db.Sections.FirstOrDefaultAsync(s => s.Key == RetiredQuoteKey, ct);
        if (quote is null)
        {
            return;
        }

        if (!await db.Sections.AnyAsync(s => s.Key == SectionSchemas.Contact, ct))
        {
            var schema = SectionSchemas.Find(SectionSchemas.Contact)!;
            var ctx = new ContentValidationContext();
            var data = ContentValidator.Validate(schema.Fields, Resolve(sectionsSeed[SectionSchemas.Contact]?.DeepClone(), mediaKeys), ctx);
            EnsureValid(ctx, SectionSchemas.Contact);
            var json = data.ToJsonString();
            db.Sections.Add(new Section
            {
                Key = SectionSchemas.Contact,
                SortOrder = quote.SortOrder,
                IsVisible = quote.IsVisible,
                DraftJson = json,
                PublishedJson = json,
                DraftUpdatedAt = now,
                PublishedAt = now,
            });
        }

        db.Sections.Remove(quote);
        foreach (var section in await db.Sections.Where(s => s.Key != RetiredQuoteKey).ToListAsync(ct))
        {
            section.DraftJson = ReplaceRetiredTexts(section.DraftJson);
            if (section.PublishedJson is not null)
            {
                section.PublishedJson = ReplaceRetiredTexts(section.PublishedJson);
            }
        }

        // Visitors' cached pages are built from the old section: mark the content as changed.
        var state = await db.ContentStates.FirstOrDefaultAsync(s => s.Id == ContentVersion.RowId, ct);
        if (state is not null)
        {
            state.Version++;
            state.ChangedAt = now;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Replaced the Request a quote section with Contact us");
    }

    internal static string ReplaceRetiredTexts(string json)
    {
        var node = JsonNode.Parse(json);
        return node is not null && ReplaceRetiredTexts(node) ? node.ToJsonString() : json;
    }

    private static bool ReplaceRetiredTexts(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text) && RetiredTexts.TryGetValue(text, out var replacement))
                    {
                        obj[key] = replacement;
                        changed = true;
                    }
                    else if (obj[key] is { } child)
                    {
                        changed |= ReplaceRetiredTexts(child);
                    }
                }

                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue value && value.TryGetValue<string>(out var text) && RetiredTexts.TryGetValue(text, out var replacement))
                    {
                        arr[i] = replacement;
                        changed = true;
                    }
                    else if (arr[i] is { } child)
                    {
                        changed |= ReplaceRetiredTexts(child);
                    }
                }

                break;
        }

        return changed;
    }

    /// <summary>
    /// Creates the first administrator from configuration when the database has no users. In
    /// Development with <see cref="BootstrapOptions.SyncInDevelopment"/>, keeps that account usable instead.
    /// </summary>
    public async Task BootstrapAdminAsync(BootstrapOptions options, bool isDevelopment = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            return;
        }

        if (options.SyncInDevelopment)
        {
            if (isDevelopment)
            {
                await SyncDevelopmentAdminAsync(options, ct);
                return;
            }

            logger.LogWarning("Bootstrap:SyncInDevelopment is ignored outside Development.");
        }

        if (await db.Users.AnyAsync(ct))
        {
            logger.LogWarning("Bootstrap admin settings are present but accounts already exist; ignoring them. Remove Bootstrap__AdminEmail and Bootstrap__AdminPassword.");
            return;
        }

        var result = await CreateAdminAsync(options.AdminEmail, options.AdminName, options.AdminPassword, ct);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not create the bootstrap administrator: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        logger.LogWarning("Created the first administrator {Email} from configuration. Remove the Bootstrap settings now.", options.AdminEmail);
    }

    /// <summary>
    /// The local development sign-in: creates the account, or puts back its password, lifts a lockout,
    /// turns it on and makes it an administrator. Nothing changes (and no session ends) when it is already fine.
    /// </summary>
    private async Task SyncDevelopmentAdminAsync(BootstrapOptions options, CancellationToken ct)
    {
        var email = options.AdminEmail.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            var created = await CreateAdminAsync(email, options.AdminName, options.AdminPassword, ct);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException("Could not create the development administrator: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            }

            logger.LogWarning("Development: created the administrator {Email} from Bootstrap settings (appsettings.Development.json).", email);
            return;
        }

        var changes = new List<string>();
        if (!await users.CheckPasswordAsync(user, options.AdminPassword))
        {
            // Also lifts the lockout and writes the audit entry.
            var reset = await ResetPasswordAsync(email, options.AdminPassword, ct);
            if (!reset.Succeeded)
            {
                throw new InvalidOperationException("Could not reset the development administrator's password: " + string.Join("; ", reset.Errors.Select(e => e.Description)));
            }

            user = await users.FindByEmailAsync(email) ?? user;
            changes.Add("password reset");
        }
        else if (await users.IsLockedOutAsync(user) || await users.GetAccessFailedCountAsync(user) > 0)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
            changes.Add("unlocked");
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            await users.UpdateAsync(user);
            changes.Add("turned on");
        }

        if (!await users.IsInRoleAsync(user, Roles.Admin))
        {
            await users.AddToRoleAsync(user, Roles.Admin);
            changes.Add("made an administrator");
        }

        if (changes.Count > 0)
        {
            logger.LogWarning("Development: {Email} {Changes} from Bootstrap settings (appsettings.Development.json).", email, string.Join(", ", changes));
        }
    }

    public async Task<IdentityResult> CreateAdminAsync(string email, string? name, string password, CancellationToken ct = default)
    {
        await SeedAsync(ct);
        var normalized = email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(normalized) is not null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = $"An account for {normalized} already exists." });
        }

        var user = new AppUser
        {
            UserName = normalized,
            Email = normalized,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(name) ? "Administrator" : name.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            LockoutEnabled = true,
        };
        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await users.AddToRoleAsync(user, Roles.Admin);
        }

        return result;
    }

    /// <summary>
    /// Sets a new password for an existing account from the server console (the reset-password
    /// command), lifts a lockout, ends the account's sessions and records it in the audit log.
    /// </summary>
    public async Task<IdentityResult> ResetPasswordAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(normalized);
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = $"There is no account for {normalized}." });
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
        {
            return result;
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        db.AuditLog.Add(new AuditEntry
        {
            At = clock.GetUtcNow().UtcDateTime,
            UserEmail = "server console",
            Action = AuditActions.UserPasswordReset,
            EntityType = "user",
            EntityId = user.Id.ToString(),
            Summary = $"Reset the password of {normalized} from the server console",
        });
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>The e-mail addresses of all accounts, for the console commands' hints.</summary>
    public async Task<IReadOnlyList<string>> AccountEmailsAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking().OrderBy(u => u.Email).Select(u => u.Email ?? "").ToListAsync(ct);

    private async Task<Dictionary<string, int>> SeedMediaAsync(DateTime now, CancellationToken ct)
    {
        var seeds = ReadSeed("media.json").Deserialize<List<SeedMedia>>(JsonSerializerOptions.Web) ?? [];
        var existing = await db.Media.Where(m => m.SeedKey != null).ToDictionaryAsync(m => m.SeedKey!, ct);
        foreach (var seed in seeds.Where(s => !existing.ContainsKey(s.Key)))
        {
            var asset = new MediaAsset
            {
                Kind = MediaKind.Bundled,
                SeedKey = seed.Key,
                StoragePath = seed.Path,
                FileName = seed.FileName,
                ContentType = seed.Type,
                Width = seed.Width,
                Height = seed.Height,
                AltAr = seed.AltAr,
                AltEn = seed.AltEn,
                SourcesJson = seed.Sources.Count > 0 ? JsonSerializer.Serialize(seed.Sources, JsonSerializerOptions.Web) : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Media.Add(asset);
            existing[seed.Key] = asset;
        }

        await db.SaveChangesAsync(ct);
        return existing.ToDictionary(kv => kv.Key, kv => kv.Value.Id);
    }

    /// <summary>Replaces "$media:key" placeholders in the seed with real media ids.</summary>
    private static JsonNode? Resolve(JsonNode? node, IReadOnlyDictionary<string, int> media)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    obj[key] = Resolve(obj[key]?.DeepClone(), media);
                }

                return obj;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    arr[i] = Resolve(arr[i]?.DeepClone(), media);
                }

                return arr;
            case JsonValue v when v.TryGetValue<string>(out var s) && s.StartsWith("$media:", StringComparison.Ordinal):
                return media.TryGetValue(s["$media:".Length..], out var id) ? id : null;
            default:
                return node;
        }
    }

    private static JsonNode ReadSeed(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Seed." + name, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        return JsonNode.Parse(stream) ?? new JsonObject();
    }

    private static void EnsureValid(ContentValidationContext ctx, string what)
    {
        if (ctx.Errors.Any)
        {
            throw new InvalidOperationException($"Seed content for {what} does not match its schema: " +
                string.Join("; ", ctx.Errors.ToDictionary().Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));
        }
    }
}
