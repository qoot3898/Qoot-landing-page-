using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Qoot.Api.Data;
using Qoot.Api.Infrastructure;
using Qoot.Api.Media;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddQootServices(builder.Configuration, builder.Environment);
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

var app = builder.Build();

// Command-line tasks: `dotnet Qoot.Api.dll migrate | seed | create-admin --email ... | reset-password --email ...`
if (args.Length > 0 && Cli.IsCommand(args[0]))
{
    return await Cli.RunAsync(app, args);
}

await InitialiseDatabaseAsync(app);

var security = app.Services.GetRequiredService<IOptions<SecurityOptions>>().Value;
if (security.UseForwardedHeaders)
{
    app.UseForwardedHeaders();
}

// Request logging wraps the exception handler, so each request is logged once with the status the
// client really received. Expected API errors (wrong password, validation, not found) are exceptions
// that ApiExceptionHandler turns into 4xx problem responses; with the logger inside the handler,
// Serilog reported every one of them as "responded 500" with a stack trace. Real 500s are still
// logged with their exception by ApiExceptionHandler.
app.UseSerilogRequestLogging(o => o.GetLevel = (ctx, _, ex) =>
    ex is not null || ctx.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
    : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
    : Serilog.Events.LogEventLevel.Information);

app.UseExceptionHandler();
if (security.EnforceHttps && !app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRequestLocalization();

app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api/admin") && !ctx.Request.Path.StartsWithSegments("/api/auth"),
    branch => branch.UseResponseCompression());

// Uploaded media: generated names, so they can be cached for a year.
var media = app.Services.GetRequiredService<IMediaStorage>();
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/media",
    FileProvider = new PhysicalFileProvider(media.RootPath),
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        ctx.Context.Response.Headers[HeaderNames.ContentDisposition] = "inline";
    },
});

// The built website, when it is deployed together with the API (recommended: one origin, no CORS).
var frontend = app.Services.GetRequiredService<IOptions<FrontendOptions>>().Value;
if (FrontendHostMiddleware.FileProvider(app.Environment, frontend.DistPath) is { } site)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = site,
        OnPrepareResponse = ctx =>
        {
            var path = ctx.Context.Request.Path;
            ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                path.StartsWithSegments("/static") || path.StartsWithSegments("/admin/static") || path.StartsWithSegments("/assets")
                    ? "public, max-age=31536000, immutable"
                    : path.Value!.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "no-cache" : "public, max-age=86400";
        },
    });
    app.UseMiddleware<FrontendHostMiddleware>();
}

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api/admin") || ctx.Request.Path.StartsWithSegments("/api/auth"))
    {
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[HeaderNames.CacheControl] = "no-store";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(o => o.WithTitle("Qoot Website API")).AllowAnonymous();
}

// Unknown /api routes answer with a problem document rather than an empty 404.
app.MapFallback("/api/{**rest}", (HttpContext ctx) => ProblemWriter.WriteAsync(ctx, StatusCodes.Status404NotFound, "not_found")).AllowAnonymous();

// Anything else that is neither a file nor a page (a deleted /media file, a mistyped path) is a plain 404.
app.MapFallback("{**path}", (HttpContext ctx) =>
{
    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
}).AllowAnonymous();

await app.RunAsync();
return 0;

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<QootDbContext>();
    if (db.Database.IsRelational())
    {
        if (options.ApplyMigrationsOnStartup)
        {
            await db.Database.MigrateAsync();
        }
        else if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            app.Logger.LogCritical("The database is missing migrations. Run `dotnet Qoot.Api.dll migrate` (or set Database:ApplyMigrationsOnStartup=true) and restart.");
            return;
        }
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    if (options.SeedOnStartup)
    {
        await seeder.SeedAsync();
    }

    await seeder.BootstrapAdminAsync(app.Services.GetRequiredService<IOptions<BootstrapOptions>>().Value, app.Environment.IsDevelopment());
}

public partial class Program;
