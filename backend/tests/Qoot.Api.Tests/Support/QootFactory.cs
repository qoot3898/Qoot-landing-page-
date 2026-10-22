using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Services;

namespace Qoot.Api.Tests.Support;

/// <summary>
/// Runs the real API in memory: the same middleware, authentication, antiforgery, rate limits and
/// validation as production. Each factory gets its own database and media folder.
/// Set QOOT_TEST_SQLSERVER to a connection string to run the same tests against SQL Server instead.
/// </summary>
public sealed class QootFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@qoot.test";
    public const string AdminPassword = "Test-Admin-Pass-2026";

    private readonly string _mediaRoot = Path.Combine(Path.GetTempPath(), "qoot-tests-" + Guid.NewGuid().ToString("N"));
    /// <summary>Configuration values that replace the test defaults (for example a lower rate limit).</summary>
    public Dictionary<string, string?> Overrides { get; init; } = [];

    public FakeTime Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var sqlServer = Environment.GetEnvironmentVariable("QOOT_TEST_SQLSERVER");
        var settings = new Dictionary<string, string?>
        {
            ["Database:Provider"] = string.IsNullOrEmpty(sqlServer) ? "InMemory" : "SqlServer",
            ["Database:InMemoryName"] = "qoot-" + Guid.NewGuid().ToString("N"),
            ["Database:ApplyMigrationsOnStartup"] = "true",
            ["ConnectionStrings:Qoot"] = string.IsNullOrEmpty(sqlServer) ? "" : sqlServer.Replace("{db}", "QootTests_" + Guid.NewGuid().ToString("N")[..8], StringComparison.Ordinal),
            ["Media:RootPath"] = _mediaRoot,
            ["Frontend:DistPath"] = "",
            ["Security:EnforceHttps"] = "false",
            ["Security:PublicRequestsPerMinute"] = "10000",
            ["Security:LoginAttemptsPerMinute"] = "1000",
            ["Security:ContactRequestsPer10Minutes"] = "1000",
            ["Bootstrap:AdminEmail"] = AdminEmail,
            ["Bootstrap:AdminPassword"] = AdminPassword,
            ["Bootstrap:AdminName"] = "Test Admin",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        };
        foreach (var (key, value) in Overrides)
        {
            settings[key] = value;
        }

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public QootClient CreateQootClient(string lang = "en")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(lang));
        return new QootClient(client);
    }

    public async Task<QootClient> SignInAsync(string email = AdminEmail, string password = AdminPassword)
    {
        var client = CreateQootClient();
        var response = await client.LoginAsync(email, password);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return client;
    }

    private bool _started;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _started = true;
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        // Against SQL Server each factory made its own database: drop it.
        if (disposing && _started && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QOOT_TEST_SQLSERVER")))
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<Qoot.Api.Data.QootDbContext>().Database.EnsureDeleted();
            }
            catch (InvalidOperationException)
            {
                // the host never reached the database
            }
        }

        base.Dispose(disposing);
        try
        {
            if (Directory.Exists(_mediaRoot))
            {
                Directory.Delete(_mediaRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>A browser-like client: keeps cookies and sends the antiforgery token like the dashboard does.</summary>
public sealed class QootClient(HttpClient http) : IDisposable
{
    public HttpClient Http { get; } = http;

    public async Task RefreshAntiforgeryAsync()
    {
        var json = await Http.GetFromJsonAsync<JsonObject>("/api/auth/antiforgery");
        Http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Http.DefaultRequestHeaders.Add("X-XSRF-TOKEN", json!["token"]!.GetValue<string>());
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password, bool remember = false)
    {
        await RefreshAntiforgeryAsync();
        var response = await Http.PostAsJsonAsync("/api/auth/login", new { email, password, rememberMe = remember });
        if (response.IsSuccessStatusCode)
        {
            // The token is bound to the signed-in user, as in the dashboard.
            await RefreshAntiforgeryAsync();
        }

        return response;
    }

    public async Task<JsonNode> GetJsonAsync(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Http.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
        return JsonNode.Parse(body.Length == 0 ? "{}" : body)!;
    }

    public async Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
        }

        var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
    }

    public Task<(HttpStatusCode Status, JsonNode? Body)> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

    public Task<(HttpStatusCode Status, JsonNode? Body)> PutAsync(string url, object? body = null) => SendAsync(HttpMethod.Put, url, body);

    public Task<(HttpStatusCode Status, JsonNode? Body)> PatchAsync(string url, object? body = null) => SendAsync(HttpMethod.Patch, url, body);

    public Task<(HttpStatusCode Status, JsonNode? Body)> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url);

    public async Task<JsonNode> SiteAsync(string lang = "en") => await GetJsonAsync($"/api/public/site?lang={lang}");

    public void Dispose() => Http.Dispose();
}

/// <summary>A clock tests can move forward (scheduled articles, session limits).</summary>
public sealed class FakeTime : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

public static class JsonExtensions
{
    public static JsonNode Section(this JsonNode site, string key) =>
        site["sections"]!.AsArray().First(s => s!["key"]!.GetValue<string>() == key)!["data"]!;

    public static string Str(this JsonNode? node, string key) => node?[key]?.GetValue<string>() ?? "";

    public static JsonObject CloneObject(this JsonNode node) => (JsonObject)node.DeepClone();
}
