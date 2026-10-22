using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Tests.Support;

namespace Qoot.Api.Tests;

public static class TestImages
{
    public static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAUAAAAECAIAAADJUWIXAAAAFElEQVR4nGNk8PRkQAJMDKiAEB8AJFMAmh7m+AcAAAAASUVORK5CYII=");
    public static readonly byte[] Jpeg = Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wAARCAAEAAUDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDxiiiiu4+VP//Z");
    public static readonly byte[] Gif = Convert.FromBase64String("R0lGODdhBQAEAIEAAAAzMwBmZgAAAAAAACwAAAAABQAEAAAIDgABBAAgsOBAgwQPBggIADs=");
    public static readonly byte[] WebP = Convert.FromBase64String("UklGRjQAAABXRUJQVlA4ICgAAABwAQCdASoFAAQAAUAmJaACdAGIQAD++Op+rLUQ5H/+Ur/43f7st4AA");
    public static readonly byte[] WebPLossless = Convert.FromBase64String("UklGRhwAAABXRUJQVlA4TBAAAAAvBMAAAAfQpGhS/wMR0f8A");
    public static readonly byte[] AvifHead = Convert.FromBase64String("AAAAHGZ0eXBhdmlmAAAAAG1pZjFhdmlmbWlhZgAAANZtZXRhAAAAAAAAACFoZGxyAAAAAAAAAABwaWN0AAAAAAAAAAAAAAAAAAAAACJpbG9jAAAAAERAAAEAAQAAAAAA+gABAAAAAAAAHywAAAAjaWluZgAAAAAAAQAAABVpbmZlAgAAAAABAABhdjAxAAAAAA5waXRtAAAAAAABAAAAVmlwcnAAAAA4aXBjbwAAAAxhdjFDgSECAAAAABRpc3BlAAAAAAAAAoAAAAGqAAAAEHBpeGkAAAAAAwgICAAAABZpcG1h");

    public static MultipartFormDataContent Form(string field, params (string Name, byte[] Bytes, string Type)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, bytes, type) in files)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(type);
            form.Add(part, field, name);
        }

        return form;
    }
}

public sealed class MediaTests
{
    private static async Task<JsonNode> UploadAsync(QootClient admin, string name, byte[] bytes, string type = "image/png", HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var form = TestImages.Form("files", (name, bytes, type));
        form.Add(new StringContent("وصف"), "altAr");
        form.Add(new StringContent("Description"), "altEn");
        var response = await admin.Http.PostAsync("/api/admin/media", form);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return JsonNode.Parse(text)!;
    }

    [Fact]
    public async Task Images_are_identified_from_their_bytes_and_served_from_media()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var uploaded = (await UploadAsync(admin, "photo.png", TestImages.Png))[0]!;
        uploaded.Str("contentType").ShouldBe("image/png");
        uploaded["width"]!.GetValue<int>().ShouldBe(5);
        uploaded["height"]!.GetValue<int>().ShouldBe(4);
        uploaded.Str("altEn").ShouldBe("Description");
        var url = uploaded.Str("url");
        url.ShouldStartWith("/media/");
        url.ShouldEndWith(".png");

        using var visitor = factory.CreateQootClient();
        var file = await visitor.Http.GetAsync(url);
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        file.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        (await file.Content.ReadAsByteArrayAsync()).ShouldBe(TestImages.Png);
    }

    [Fact]
    public async Task A_file_named_png_that_is_not_an_image_or_an_svg_is_refused()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var fake = "<html><script>alert(1)</script></html>                                  "u8.ToArray();
        var refused = await UploadAsync(admin, "evil.png", fake, "image/png", HttpStatusCode.BadRequest);
        refused.Str("code").ShouldBe("validation_failed");

        var svg = "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'><rect width='9' height='9'/></svg>"u8.ToArray();
        await UploadAsync(admin, "drawing.svg", svg, "image/svg+xml", HttpStatusCode.BadRequest);

        // Nothing was stored.
        (await admin.GetJsonAsync("/api/admin/media?kind=upload"))["total"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public async Task Oversized_uploads_are_refused()
    {
        using var factory = new QootFactory { Overrides = new() { ["Media:MaxUploadBytes"] = "100" } };
        using var admin = await factory.SignInAsync();
        var big = TestImages.Png.Concat(new byte[200]).ToArray();
        var refused = await UploadAsync(admin, "big.png", big, "image/png", HttpStatusCode.BadRequest);
        refused["errors"]!["files[0]"].ShouldNotBeNull();
    }

    [Fact]
    public async Task Pictures_in_use_cannot_be_deleted_and_replacing_keeps_every_reference_working()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var uploaded = (await UploadAsync(admin, "slide.jpg", TestImages.Jpeg, "image/jpeg"))[0]!;
        var id = uploaded["id"]!.GetValue<int>();

        var hero = await admin.GetJsonAsync("/api/admin/sections/hero");
        var draft = hero["draft"]!.CloneObject();
        draft["slides"]![0]!["image"] = id;
        (await admin.PutAsync("/api/admin/sections/hero/draft", new { data = draft, concurrencyStamp = hero["concurrencyStamp"]!.GetValue<Guid>() })).Status.ShouldBe(HttpStatusCode.OK);
        await admin.PostAsync("/api/admin/sections/hero/publish");

        var (deleteStatus, deleteBody) = await admin.DeleteAsync($"/api/admin/media/{id}");
        deleteStatus.ShouldBe(HttpStatusCode.Conflict);
        deleteBody.Str("code").ShouldBe("media_in_use");
        (await admin.GetJsonAsync($"/api/admin/media/{id}/usage")).AsArray().Single()!.Str("key").ShouldBe("hero");

        using var form = TestImages.Form("file", ("new.webp", TestImages.WebP, "image/webp"));
        var replaced = await admin.Http.PostAsync($"/api/admin/media/{id}/replace", form);
        replaced.StatusCode.ShouldBe(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());

        using var visitor = factory.CreateQootClient();
        var slide = (await visitor.SiteAsync()).Section("hero")["slides"]![0]!["image"]!;
        slide["id"]!.GetValue<int>().ShouldBe(id);
        slide.Str("type").ShouldBe("image/webp");
        slide.Str("url").ShouldEndWith(".webp");
        (await visitor.Http.GetAsync(slide.Str("url"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await visitor.Http.GetAsync(uploaded.Str("url"))).StatusCode.ShouldBe(HttpStatusCode.NotFound); // old file removed
    }

    [Fact]
    public async Task Brand_files_cannot_be_deleted_but_their_descriptions_can_be_edited()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var bundled = (await admin.GetJsonAsync("/api/admin/media?kind=bundled&pageSize=50"))["items"]!.AsArray();
        bundled.Count.ShouldBe(23);
        var first = bundled.First(m => m.Str("fileName") == "qoot-paper-bags.webp")!;
        var id = first["id"]!.GetValue<int>();
        (await admin.DeleteAsync($"/api/admin/media/{id}")).Body.Str("code").ShouldBe("media_bundled");

        var (status, updated) = await admin.PutAsync($"/api/admin/media/{id}", new { altAr = "أكياس ورقية", altEn = "Paper bags" });
        status.ShouldBe(HttpStatusCode.OK);
        updated.Str("altEn").ShouldBe("Paper bags");
    }

    [Fact]
    public async Task Unused_uploads_can_be_deleted()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var uploaded = (await UploadAsync(admin, "spare.gif", TestImages.Gif, "image/gif"))[0]!;
        (await admin.DeleteAsync($"/api/admin/media/{uploaded["id"]}")).Status.ShouldBe(HttpStatusCode.NoContent);
        using var visitor = factory.CreateQootClient();
        (await visitor.Http.GetAsync(uploaded.Str("url"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Drawings_are_refused_where_a_photograph_is_expected()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var drawing = (await admin.GetJsonAsync("/api/admin/media?type=drawing&pageSize=50"))["items"]![0]!["id"]!.GetValue<int>();
        var hero = await admin.GetJsonAsync("/api/admin/sections/hero");
        var draft = hero["draft"]!.CloneObject();
        draft["slides"]![0]!["image"] = drawing;
        var (status, body) = await admin.PutAsync("/api/admin/sections/hero/draft", new { data = draft, concurrencyStamp = hero["concurrencyStamp"]!.GetValue<Guid>() });
        status.ShouldBe(HttpStatusCode.BadRequest);
        body!["errors"]!["slides[0].image"].ShouldNotBeNull();
    }
}

public sealed class ContactTests
{
    private static object Request(string phone = "+218 91 234 5678", string name = "Salem", string message = "We need daily meals for 120 people in Brega.", string email = "salem@example.com", string? website = null) => new
    {
        name, phone, email, message, language = "en", website,
    };

    [Fact]
    public async Task Contact_us_messages_are_stored_and_managed_in_the_dashboard()
    {
        using var factory = new QootFactory();
        using var visitor = factory.CreateQootClient();
        var (status, _) = await visitor.PostAsync("/api/public/contact", Request());
        status.ShouldBe(HttpStatusCode.Accepted);

        using var admin = await factory.SignInAsync();
        var list = await admin.GetJsonAsync("/api/admin/messages?status=unread");
        var item = list["items"]!.AsArray().Single()!;
        item.Str("name").ShouldBe("Salem");
        item.Str("phone").ShouldBe("+218 91 234 5678");
        item.Str("email").ShouldBe("salem@example.com");
        item.Str("message").ShouldContain("Brega");
        var id = item["id"]!.GetValue<int>();

        (await admin.GetJsonAsync($"/api/admin/messages/{id}"))["isRead"]!.GetValue<bool>().ShouldBeTrue();
        var (_, updated) = await admin.PatchAsync($"/api/admin/messages/{id}", new { status = "in_progress", adminNotes = "Called back" });
        updated.Str("status").ShouldBe("in_progress");
        (await admin.GetJsonAsync("/api/admin/messages?search=Brega"))["total"]!.GetValue<int>().ShouldBe(1);
        (await admin.GetJsonAsync("/api/admin/dashboard"))["messages"]!["total"]!.GetValue<int>().ShouldBe(1);
        (await admin.DeleteAsync($"/api/admin/messages/{id}")).Status.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_email_is_optional()
    {
        using var factory = new QootFactory();
        using var visitor = factory.CreateQootClient();
        (await visitor.PostAsync("/api/public/contact", Request(email: ""))).Status.ShouldBe(HttpStatusCode.Accepted);

        using var admin = await factory.SignInAsync();
        var item = (await admin.GetJsonAsync("/api/admin/messages"))["items"]!.AsArray().Single()!;
        item.Str("email").ShouldBe("");
    }

    [Fact]
    public async Task Messages_are_validated_with_messages_in_the_visitors_language()
    {
        using var factory = new QootFactory();
        using var visitor = factory.CreateQootClient("ar");
        var (status, body) = await visitor.PostAsync("/api/public/contact", Request(phone: "not a phone", name: "", message: "", email: "not-an-email"));
        status.ShouldBe(HttpStatusCode.BadRequest);
        var errors = body!["errors"]!.AsObject();
        errors.ContainsKey("name").ShouldBeTrue();
        errors.ContainsKey("message").ShouldBeTrue();
        errors.ContainsKey("email").ShouldBeTrue();
        errors["phone"]![0]!.GetValue<string>().ShouldContain("هاتف");

        var (missing, missingBody) = await visitor.PostAsync("/api/public/contact", Request(phone: ""));
        missing.ShouldBe(HttpStatusCode.BadRequest);
        missingBody!["errors"]!.AsObject().ContainsKey("phone").ShouldBeTrue();
    }

    [Fact]
    public async Task Bots_filling_the_hidden_field_are_quietly_ignored()
    {
        using var factory = new QootFactory();
        using var visitor = factory.CreateQootClient();
        (await visitor.PostAsync("/api/public/contact", Request(website: "http://spam.example"))).Status.ShouldBe(HttpStatusCode.Accepted);
        using var admin = await factory.SignInAsync();
        (await admin.GetJsonAsync("/api/admin/messages"))["total"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public async Task The_form_is_rate_limited()
    {
        using var factory = new QootFactory { Overrides = new() { ["Security:ContactRequestsPer10Minutes"] = "2" } };
        using var visitor = factory.CreateQootClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await visitor.PostAsync("/api/public/contact", Request(phone: $"+218 91 000 000{i}"))).Status);
        }

        statuses.Count(s => s == HttpStatusCode.Accepted).ShouldBe(2);
        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_site_created_with_the_quote_section_gets_contact_us_in_its_place()
    {
        using var factory = new QootFactory();
        int sortOrder;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The database as it was before Contact us: a hidden "quote" section, links to #quote.
            var db = scope.ServiceProvider.GetRequiredService<QootDbContext>();
            var contact = await db.Sections.SingleAsync(s => s.Key == "contact");
            sortOrder = contact.SortOrder;
            db.Sections.Remove(contact);
            db.Sections.Add(new Section { Key = DataSeeder.RetiredQuoteKey, SortOrder = sortOrder, IsVisible = false, DraftJson = "{}", PublishedJson = "{}", DraftUpdatedAt = DateTime.UtcNow });
            var header = await db.Sections.SingleAsync(s => s.Key == "header");
            var data = JsonNode.Parse(header.DraftJson)!.AsObject();
            data["ctaTarget"] = "#quote";
            data["ctaLabel"] = new JsonObject { ["ar"] = "اطلبوا الآن", ["en"] = "Request a quote" };
            header.DraftJson = data.ToJsonString();
            header.PublishedJson = header.DraftJson;
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QootDbContext>();
            (await db.Sections.AnyAsync(s => s.Key == DataSeeder.RetiredQuoteKey)).ShouldBeFalse();
            var contact = await db.Sections.SingleAsync(s => s.Key == "contact");
            contact.SortOrder.ShouldBe(sortOrder);
            contact.IsVisible.ShouldBeFalse();
            var header = JsonNode.Parse((await db.Sections.SingleAsync(s => s.Key == "header")).PublishedJson!)!;
            header.Str("ctaTarget").ShouldBe("#contact");
            header["ctaLabel"].Str("en").ShouldBe("Contact us");
            // A text an editor wrote is left alone.
            header["ctaLabel"].Str("ar").ShouldBe("اطلبوا الآن");
        }
    }
}

public sealed class SettingsTests
{
    [Fact]
    public async Task Contact_details_changed_in_settings_reach_the_public_site()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var settings = await admin.GetJsonAsync("/api/admin/settings");
        var data = settings["data"]!.CloneObject();
        data["phone"] = "+218912345678";
        data["phoneDisplay"] = "+218 91 234 5678";
        data["notifyEmail"] = "orders@qoot.com";
        var (status, saved) = await admin.PutAsync("/api/admin/settings", new { data, concurrencyStamp = settings["concurrencyStamp"]!.GetValue<Guid>() });
        status.ShouldBe(HttpStatusCode.OK, saved?.ToJsonString());

        using var visitor = factory.CreateQootClient();
        var site = await visitor.SiteAsync();
        site["settings"].Str("phoneDisplay").ShouldBe("+218 91 234 5678");
        site["settings"]!.AsObject().ContainsKey("notifyEmail").ShouldBeFalse();
        var organization = site["seo"]!["jsonLd"]!["@graph"]!.AsArray().First(g => g.Str("@type") == "Organization");
        organization.Str("telephone").ShouldBe("+218912345678");
    }

    [Fact]
    public async Task Arabic_cannot_be_turned_off_and_invalid_values_are_refused()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var settings = await admin.GetJsonAsync("/api/admin/settings");
        var data = settings["data"]!.CloneObject();
        data["enabledLanguages"] = new JsonArray("en");
        data["email"] = "not-an-email";
        data["siteUrl"] = "javascript:alert(1)";
        var (status, body) = await admin.PutAsync("/api/admin/settings", new { data, concurrencyStamp = settings["concurrencyStamp"]!.GetValue<Guid>() });
        status.ShouldBe(HttpStatusCode.BadRequest);
        var errors = body!["errors"]!.AsObject();
        errors.ContainsKey("enabledLanguages").ShouldBeTrue();
        errors.ContainsKey("email").ShouldBeTrue();
        errors.ContainsKey("siteUrl").ShouldBeTrue();
    }

    [Fact]
    public async Task Turning_english_off_removes_it_from_the_language_list()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var settings = await admin.GetJsonAsync("/api/admin/settings");
        var data = settings["data"]!.CloneObject();
        data["enabledLanguages"] = new JsonArray("ar");
        (await admin.PutAsync("/api/admin/settings", new { data, concurrencyStamp = settings["concurrencyStamp"]!.GetValue<Guid>() })).Status.ShouldBe(HttpStatusCode.OK);
        using var visitor = factory.CreateQootClient();
        (await visitor.SiteAsync("en"))["languages"]!.AsArray().Select(l => l.Str("code")).ShouldBe(["ar"]);
    }
}
