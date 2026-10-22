using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Qoot.Api.Controllers;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Media;
using Qoot.Api.Services;

namespace Qoot.Api.Infrastructure;

public static class RateLimits
{
    public const string Public = "public";
    public const string Contact = "contact";
    public const string Login = "login";
    public const string Upload = "upload";
}

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Origins allowed to call the API from a browser with credentials (only needed when the site is on another origin).</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Strict (same site only, recommended), Lax or None (cross-site; requires HTTPS).</summary>
    public string CookieSameSite { get; set; } = "Strict";

    /// <summary>Minutes of inactivity before the dashboard session ends.</summary>
    public int IdleTimeoutMinutes { get; set; } = 60;

    /// <summary>Hours after sign-in when a session ends regardless of activity ("remember me" sessions: days below).</summary>
    public int AbsoluteSessionHours { get; set; } = 12;

    public int RememberMeDays { get; set; } = 7;

    /// <summary>Trust X-Forwarded-For/Proto from the reverse proxy in front of the app.</summary>
    public bool UseForwardedHeaders { get; set; }

    /// <summary>Redirect HTTP to HTTPS and send HSTS (turn off only when TLS ends at a proxy that already does this).</summary>
    public bool EnforceHttps { get; set; } = true;

    public int LoginAttemptsPerMinute { get; set; } = 5;
    public int ContactRequestsPer10Minutes { get; set; } = 5;
    public int PublicRequestsPerMinute { get; set; } = 300;
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>SqlServer (default) or InMemory (development and automated tests only: nothing is kept).</summary>
    public string Provider { get; set; } = "SqlServer";

    public bool ApplyMigrationsOnStartup { get; set; }
    public bool SeedOnStartup { get; set; } = true;
}

public static class ServiceSetup
{
    public static IServiceCollection AddQootServices(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.Configure<MediaOptions>(config.GetSection(MediaOptions.SectionName));
        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.SectionName));
        services.Configure<FrontendOptions>(config.GetSection(FrontendOptions.SectionName));
        services.Configure<BootstrapOptions>(config.GetSection(BootstrapOptions.SectionName));
        // Options are read when first needed (not here), so test hosts and environment variables can override them.
        services.AddOptions<SecurityOptions>().Bind(config.GetSection(SecurityOptions.SectionName));
        services.AddOptions<DatabaseOptions>().Bind(config.GetSection(DatabaseOptions.SectionName));

        // ---------------------------------------------------------------- data
        services.AddDbContext<QootDbContext>((sp, options) =>
        {
            var database = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var configuration = sp.GetRequiredService<IConfiguration>();
            if (string.Equals(database.Provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                if (sp.GetRequiredService<IHostEnvironment>().IsProduction())
                {
                    throw new InvalidOperationException("The InMemory database keeps nothing and is refused in Production. Set Database:Provider to SqlServer.");
                }

                options.UseInMemoryDatabase(configuration["Database:InMemoryName"] ?? "qoot");
            }
            else
            {
                var connection = configuration.GetConnectionString("Qoot");
                if (string.IsNullOrWhiteSpace(connection))
                {
                    throw new InvalidOperationException("ConnectionStrings:Qoot is not set. See backend/README.md.");
                }

                options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure(5).MigrationsHistoryTable("__EFMigrationsHistory"));
            }
        });

        // ---------------------------------------------------------------- data protection
        // The keys that sign the sign-in and antiforgery cookies. Kept on disk (DataProtection:KeysPath,
        // default App_Data/keys) so sessions survive restarts and deployments; encrypted with DPAPI on
        // Windows. Point it at persistent storage in containers. Tests keep keys in memory.
        services.AddDataProtection().SetApplicationName("Qoot");
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp => new ConfigureOptions<KeyManagementOptions>(o =>
        {
            var hostEnv = sp.GetRequiredService<IHostEnvironment>();
            if (hostEnv.IsEnvironment("Testing"))
            {
                return;
            }

            var configured = sp.GetRequiredService<IConfiguration>()["DataProtection:KeysPath"];
            var path = string.IsNullOrWhiteSpace(configured) ? "App_Data/keys" : configured;
            var full = Path.IsPathRooted(path) ? path : Path.Combine(hostEnv.ContentRootPath, path);
            Directory.CreateDirectory(full);
            var loggers = sp.GetRequiredService<ILoggerFactory>();
            o.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(full), loggers);
            if (OperatingSystem.IsWindows())
            {
                o.XmlEncryptor = new DpapiXmlEncryptor(protectToLocalMachine: true, loggers);
            }
        }));

        // ---------------------------------------------------------------- identity
        services.AddIdentity<AppUser, AppRole>(o =>
            {
                o.Password.RequiredLength = PasswordRules.MinLength;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 5;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<QootDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<SecurityStampValidatorOptions>(o =>
        {
            // Turning an account off, changing its role or resetting its password ends its sessions within a minute.
            o.ValidationInterval = TimeSpan.FromMinutes(1);
            o.OnRefreshingPrincipal = ctx =>
            {
                if (ctx.CurrentPrincipal?.FindFirst(AuthController.LoginAtClaim) is { } claim && ctx.NewPrincipal?.Identity is ClaimsIdentity identity)
                {
                    identity.AddClaim(new Claim(AuthController.LoginAtClaim, claim.Value));
                }

                return Task.CompletedTask;
            };
        });

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<SecurityOptions>, IHostEnvironment>((o, securityOptions, hostEnv) =>
            {
                var security = securityOptions.Value;
                o.Cookie.Name = "qoot.auth";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = ParseSameSite(security.CookieSameSite);
                o.Cookie.SecurePolicy = IsLocal(hostEnv) ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(security.IdleTimeoutMinutes);
                o.SlidingExpiration = true;
                o.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx => ProblemWriter.WriteAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, "unauthorized"),
                    OnRedirectToAccessDenied = ctx => ProblemWriter.WriteAsync(ctx.HttpContext, StatusCodes.Status403Forbidden, "forbidden"),
                    OnValidatePrincipal = async ctx =>
                    {
                        await SecurityStampValidator.ValidatePrincipalAsync(ctx);
                        if (ctx.Principal is null)
                        {
                            return;
                        }

                        if (ctx.Principal.FindFirstValue(AuthController.LoginAtClaim) is not { } loginAt
                            || !long.TryParse(loginAt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                        {
                            ctx.RejectPrincipal();
                            return;
                        }

                        var limit = ctx.Properties.IsPersistent ? TimeSpan.FromDays(security.RememberMeDays) : TimeSpan.FromHours(security.AbsoluteSessionHours);
                        var clock = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                        if (clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(seconds) > limit)
                        {
                            ctx.RejectPrincipal();
                            await ctx.HttpContext.RequestServices.GetRequiredService<SignInManager<AppUser>>().SignOutAsync();
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.ContentEditor, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin, Roles.Editor))
            .AddPolicy(Policies.Administrator, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin))
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>()
            .Configure<IOptions<SecurityOptions>, IHostEnvironment>((o, securityOptions, hostEnv) =>
            {
                o.HeaderName = "X-XSRF-TOKEN";
                o.Cookie.Name = "qoot.af";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = ParseSameSite(securityOptions.Value.CookieSameSite);
                o.Cookie.SecurePolicy = IsLocal(hostEnv) ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            });

        // ---------------------------------------------------------------- web
        services.AddControllers(o => o.Filters.Add<AntiforgeryFilter>())
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
                o.JsonSerializerOptions.Encoder = Content.JsonDefaults.Encoder;
            })
            .ConfigureApiBehaviorOptions(o =>
            {
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var errors = ctx.ModelState.Where(kv => kv.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kv => string.IsNullOrEmpty(kv.Key) ? "body" : JsonNamingPolicy.CamelCase.ConvertName(kv.Key.TrimStart('$', '.')),
                            _ => new[] { Messages.Get("invalid_request") });
                    var problem = new ProblemDetails { Status = 400, Title = Messages.Get("invalid_request"), Type = "https://httpstatuses.io/400" };
                    problem.Extensions["code"] = "invalid_request";
                    problem.Extensions["errors"] = errors;
                    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
                };
            });

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi("v1", o => o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "Qoot Website API";
            doc.Info.Description = "Public content for www.qoot.com and the endpoints behind the dashboard. Dashboard calls need the session cookie and the X-XSRF-TOKEN header.";
            doc.Info.Version = "v1";
            return Task.CompletedTask;
        }));

        services.AddCors();

        // Compression: applied to public responses only (see Program.cs); dashboard and sign-in
        // responses carry tokens and are left uncompressed (BREACH).
        services.AddResponseCompression(o =>
        {
            o.EnableForHttps = true;
            o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            o.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
                .Concat(["image/svg+xml", "application/manifest+json"]);
        });
        services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
        services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
        services.AddOptions<CorsOptions>().Configure<IOptions<SecurityOptions>>((o, securityOptions) => o.AddDefaultPolicy(p =>
        {
            var origins = securityOptions.Value.AllowedOrigins;
            if (origins.Length > 0)
            {
                p.WithOrigins(origins)
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders("Content-Type", "X-XSRF-TOKEN", "Accept-Language", "If-None-Match")
                    .WithExposedHeaders("ETag")
                    .AllowCredentials()
                    .SetPreflightMaxAge(TimeSpan.FromHours(1));
            }
        }));

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await ProblemWriter.WriteAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests, "rate_limited");
            };
            o.AddPolicy(RateLimits.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Security(ctx).LoginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            }));
            o.AddPolicy(RateLimits.Contact, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Security(ctx).ContactRequestsPer10Minutes, Window = TimeSpan.FromMinutes(10), QueueLimit = 0,
            }));
            o.AddPolicy(RateLimits.Public, ctx => RateLimitPartition.GetSlidingWindowLimiter(ClientKey(ctx), _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = Security(ctx).PublicRequestsPerMinute, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0,
            }));
            o.AddPolicy(RateLimits.Upload, ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.Identity?.Name ?? ClientKey(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            }));
        });

        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.ForwardLimit = 2;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            foreach (var proxy in config.GetSection("Security:KnownProxies").Get<string[]>() ?? [])
            {
                if (IPAddress.TryParse(proxy, out var ip))
                {
                    o.KnownProxies.Add(ip);
                }
            }
        });

        services.Configure<RequestLocalizationOptions>(o =>
        {
            var cultures = new[] { new CultureInfo("ar"), new CultureInfo("en") };
            o.DefaultRequestCulture = new RequestCulture("ar");
            o.SupportedCultures = cultures;
            o.SupportedUICultures = cultures;
            o.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" });
        });

        services.AddMemoryCache(o => o.SizeLimit = 1000);
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

        // ---------------------------------------------------------------- app services
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IMediaStorage, LocalDiskMediaStorage>();
        services.AddSingleton<MediaUrls>();
        services.AddSingleton<SeoBuilder>();
        services.AddSingleton<NotificationQueue>();
        services.AddHostedService<ContactNotificationService>();
        services.AddScoped<ContentVersion>();
        services.AddScoped<AuditService>();
        services.AddScoped<SectionService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<NewsService>();
        services.AddScoped<MediaService>();
        services.AddScoped<ContactService>();
        services.AddScoped<UserService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<PublicSiteService>();
        services.AddScoped<PageSeoService>();
        services.AddScoped<DataSeeder>();
        return services;
    }

    private static SecurityOptions Security(HttpContext ctx) => ctx.RequestServices.GetRequiredService<IOptions<SecurityOptions>>().Value;

    private static bool IsLocal(IHostEnvironment env) => env.IsDevelopment() || env.IsEnvironment("Testing");

    /// <summary>Rate-limit partition: the client's address (the real one when forwarded headers are trusted).</summary>
    private static string ClientKey(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static SameSiteMode ParseSameSite(string value) => value.ToLowerInvariant() switch
    {
        "lax" => SameSiteMode.Lax,
        "none" => SameSiteMode.None,
        _ => SameSiteMode.Strict,
    };
}

public sealed class DatabaseHealthCheck(QootDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unreachable");
}
