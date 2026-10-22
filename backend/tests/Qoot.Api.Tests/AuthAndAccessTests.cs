using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Tests.Support;

namespace Qoot.Api.Tests;

public sealed class AuthAndAccessTests
{
    private const string EditorPassword = "Editor-Pass-2026x";

    private static readonly string[] AdminGetEndpoints =
    [
        "/api/admin/dashboard", "/api/admin/sections", "/api/admin/sections/hero", "/api/admin/news", "/api/admin/media",
        "/api/admin/messages", "/api/admin/settings", "/api/admin/users", "/api/admin/audit", "/api/admin/schemas",
        "/api/admin/news-categories", "/api/admin/preview/site", "/api/auth/me",
    ];

    [Fact]
    public async Task Anonymous_visitors_get_401_on_every_dashboard_endpoint()
    {
        using var factory = new QootFactory();
        using var client = factory.CreateQootClient();
        foreach (var url in AdminGetEndpoints)
        {
            var response = await client.Http.GetAsync(url);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, url);
        }

        await client.RefreshAntiforgeryAsync();
        (await client.PostAsync("/api/admin/sections/hero/publish")).Status.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync("/api/admin/media/1")).Status.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/admin/users", new { email = "x@y.com", password = "Whatever-Pass-1" })).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task There_is_no_public_registration_endpoint()
    {
        using var factory = new QootFactory();
        using var client = factory.CreateQootClient();
        await client.RefreshAntiforgeryAsync();
        foreach (var url in new[] { "/api/auth/register", "/api/register", "/api/auth/signup" })
        {
            (await client.PostAsync(url, new { email = "x@y.com", password = "Whatever-Pass-1" })).Status.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Writes_without_the_antiforgery_token_are_refused()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        admin.Http.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var (status, body) = await admin.PostAsync("/api/admin/sections/hero/publish");
        status.ShouldBe(HttpStatusCode.BadRequest);
        body.Str("code").ShouldBe("antiforgery");

        // A token minted for an anonymous visitor does not work for a signed-in user.
        using var other = factory.CreateQootClient();
        await other.RefreshAntiforgeryAsync();
        admin.Http.DefaultRequestHeaders.Add("X-XSRF-TOKEN", other.Http.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").First());
        (await admin.PostAsync("/api/admin/sections/hero/publish")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wrong_passwords_get_one_generic_answer_and_lock_the_account_after_five()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (_, editor) = await admin.PostAsync("/api/admin/users", new { email = "locked@qoot.test", displayName = "Locked", role = "Editor", password = EditorPassword });
        var editorId = editor!["id"]!.GetValue<Guid>();
        using var client = factory.CreateQootClient();

        var unknown = await client.LoginAsync("nobody@qoot.test", "Whatever-Pass-1");
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var unknownCode = (await unknown.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>())!.Str("code");

        for (var i = 0; i < 4; i++)
        {
            var wrong = await client.LoginAsync("locked@qoot.test", "Wrong-Password-1");
            wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await wrong.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>())!.Str("code").ShouldBe(unknownCode);
        }

        var fifth = await client.LoginAsync("locked@qoot.test", "Wrong-Password-1");
        fifth.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await fifth.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>())!.Str("code").ShouldBe("locked_out");

        // Even the right password is refused while locked.
        (await client.LoginAsync("locked@qoot.test", EditorPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await admin.GetJsonAsync("/api/admin/users")).AsArray().First(u => u.Str("email") == "locked@qoot.test")!["isLockedOut"]!.GetValue<bool>().ShouldBeTrue();

        // An administrator can lift the lock.
        (await admin.PostAsync($"/api/admin/users/{editorId}/unlock")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await client.LoginAsync("locked@qoot.test", EditorPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_attempts_are_rate_limited_per_address()
    {
        using var factory = new QootFactory { Overrides = new() { ["Security:LoginAttemptsPerMinute"] = "3" } };
        using var client = factory.CreateQootClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.LoginAsync("nobody@qoot.test", "Whatever-Pass-1")).StatusCode);
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Session_cookie_is_http_only_and_same_site_and_logout_ends_it()
    {
        using var factory = new QootFactory();
        using var client = factory.CreateQootClient();
        await client.RefreshAntiforgeryAsync();
        var response = await client.Http.PostAsJsonAsync("/api/auth/login", new { email = QootFactory.AdminEmail, password = QootFactory.AdminPassword });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("qoot.auth=", StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);

        await client.RefreshAntiforgeryAsync();
        (await client.PostAsync("/api/auth/logout")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await client.Http.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessions_end_after_the_absolute_limit()
    {
        using var factory = new QootFactory { Overrides = new() { ["Security:AbsoluteSessionHours"] = "2", ["Security:IdleTimeoutMinutes"] = "600" } };
        using var admin = await factory.SignInAsync();
        (await admin.Http.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Clock.Advance(TimeSpan.FromHours(3));
        (await admin.Http.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Editors_manage_content_but_not_users_settings_or_the_audit_log()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (created, user) = await admin.PostAsync("/api/admin/users", new { email = "editor@qoot.test", displayName = "Editor", role = "Editor", password = EditorPassword });
        created.ShouldBe(HttpStatusCode.OK, user?.ToJsonString());

        using var editor = await factory.SignInAsync("editor@qoot.test", EditorPassword);
        (await editor.Http.GetAsync("/api/admin/sections")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await editor.Http.GetAsync("/api/admin/settings")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await editor.Http.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await editor.Http.GetAsync("/api/admin/audit")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var settings = await editor.GetJsonAsync("/api/admin/settings");
        (await editor.PutAsync("/api/admin/settings", new { data = settings["data"], concurrencyStamp = settings["concurrencyStamp"]!.GetValue<Guid>() })).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await editor.PostAsync("/api/admin/users", new { email = "x@qoot.test", role = "Admin", password = EditorPassword })).Status.ShouldBe(HttpStatusCode.Forbidden);

        var dashboard = await editor.GetJsonAsync("/api/admin/dashboard");
        dashboard["recentActivity"].ShouldBeNull();
    }

    [Fact]
    public async Task Turning_an_account_off_ends_its_session_and_blocks_sign_in()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (_, user) = await admin.PostAsync("/api/admin/users", new { email = "temp@qoot.test", displayName = "Temp", role = "Editor", password = EditorPassword });
        var id = user!["id"]!.GetValue<Guid>();
        using var editor = await factory.SignInAsync("temp@qoot.test", EditorPassword);

        (await admin.PutAsync($"/api/admin/users/{id}", new { isActive = false })).Status.ShouldBe(HttpStatusCode.OK);
        factory.Clock.Advance(TimeSpan.FromMinutes(2)); // security stamp is re-checked every minute
        (await editor.Http.GetAsync("/api/admin/sections")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var again = factory.CreateQootClient();
        var login = await again.LoginAsync("temp@qoot.test", EditorPassword);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>())!.Str("code").ShouldBe("account_disabled");
    }

    [Fact]
    public async Task The_last_administrator_cannot_be_removed_or_demoted()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var me = await admin.GetJsonAsync("/api/auth/me");
        var id = me["id"]!.GetValue<Guid>();
        (await admin.DeleteAsync($"/api/admin/users/{id}")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PutAsync($"/api/admin/users/{id}", new { role = "Editor" })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Weak_passwords_are_refused_with_localised_messages()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (status, body) = await admin.PostAsync("/api/admin/users", new { email = "weak@qoot.test", role = "Editor", password = "short" });
        status.ShouldBe(HttpStatusCode.BadRequest);
        body!["errors"]!["password"]!.AsArray().Count.ShouldBeGreaterThan(0);

        admin.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        admin.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ar");
        var (_, arabic) = await admin.PostAsync("/api/admin/users", new { email = "weak@qoot.test", role = "Editor", password = "short" });
        arabic!["errors"]!["password"]![0]!.GetValue<string>().ShouldContain("كلمة المرور");
    }

    [Fact]
    public async Task Changing_the_password_keeps_the_current_session()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (wrong, _) = await admin.PostAsync("/api/auth/change-password", new { currentPassword = "nope", newPassword = "Another-Strong-Pass-1" });
        wrong.ShouldBe(HttpStatusCode.BadRequest);
        var (ok, _) = await admin.PostAsync("/api/auth/change-password", new { currentPassword = QootFactory.AdminPassword, newPassword = "Another-Strong-Pass-1" });
        ok.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Http.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var fresh = factory.CreateQootClient();
        (await fresh.LoginAsync(QootFactory.AdminEmail, "Another-Strong-Pass-1")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sensitive_actions_are_written_to_the_audit_log()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var bad = factory.CreateQootClient();
        await bad.LoginAsync(QootFactory.AdminEmail, "Wrong-Password-1");
        await admin.PostAsync("/api/admin/sections/faq/publish");

        var audit = await admin.GetJsonAsync("/api/admin/audit?pageSize=50");
        var actions = audit["items"]!.AsArray().Select(a => a.Str("action")).ToList();
        actions.ShouldContain("auth.login");
        actions.ShouldContain("auth.login_failed");
        actions.ShouldContain("section.published");
    }

    [Fact]
    public async Task The_reset_password_command_sets_a_new_password_unlocks_and_is_audited()
    {
        const string newPassword = "Console-Reset-2026x";
        using var factory = new QootFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var admin = await users.FindByEmailAsync(QootFactory.AdminEmail);
            await users.SetLockoutEndDateAsync(admin!, DateTimeOffset.UtcNow.AddYears(50));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            (await seeder.ResetPasswordAsync("nobody@qoot.test", newPassword)).Errors.Single().Code.ShouldBe("NotFound");
            (await seeder.ResetPasswordAsync(QootFactory.AdminEmail, "short")).Succeeded.ShouldBeFalse();
            (await seeder.AccountEmailsAsync()).ShouldContain(QootFactory.AdminEmail);
            (await seeder.ResetPasswordAsync(" ADMIN@qoot.test ", newPassword)).Succeeded.ShouldBeTrue();
        }

        using var client = factory.CreateQootClient();
        (await client.LoginAsync(QootFactory.AdminEmail, QootFactory.AdminPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.LoginAsync(QootFactory.AdminEmail, newPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QootDbContext>();
            var entry = await db.AuditLog.SingleAsync(a => a.Action == "user.password_reset");
            entry.UserEmail.ShouldBe("server console");
        }
    }
}
