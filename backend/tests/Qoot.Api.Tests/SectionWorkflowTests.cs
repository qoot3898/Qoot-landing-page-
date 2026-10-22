using System.Net;
using System.Text.Json.Nodes;
using Qoot.Api.Tests.Support;

namespace Qoot.Api.Tests;

/// <summary>Draft → publish workflow for page sections. Each test uses a fresh application.</summary>
public sealed class SectionWorkflowTests
{
    private static async Task<(QootFactory Factory, QootClient Admin, JsonNode Detail)> OpenAsync(string key)
    {
        var factory = new QootFactory();
        var admin = await factory.SignInAsync();
        var detail = await admin.GetJsonAsync($"/api/admin/sections/{key}");
        return (factory, admin, detail);
    }

    [Fact]
    public async Task Draft_changes_stay_private_until_published()
    {
        var (factory, admin, detail) = await OpenAsync("faq");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        draft["title"]!["en"] = "Questions people ask us";
        var (status, saved) = await admin.PutAsync("/api/admin/sections/faq/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });
        status.ShouldBe(HttpStatusCode.OK, saved?.ToJsonString());
        saved!["section"]!["hasUnpublishedChanges"]!.GetValue<bool>().ShouldBeTrue();

        using var visitor = factory.CreateQootClient();
        (await visitor.SiteAsync("en")).Section("faq").Str("title").ShouldBe("Questions we’re asked");

        (await admin.PostAsync("/api/admin/sections/faq/publish")).Status.ShouldBe(HttpStatusCode.OK);
        (await visitor.SiteAsync("en")).Section("faq").Str("title").ShouldBe("Questions people ask us");

        // Arabic was not touched.
        (await visitor.SiteAsync("ar")).Section("faq").Str("title").ShouldBe("أسئلة يطرحها عملاؤنا");
    }

    [Fact]
    public async Task Preview_shows_the_draft_to_signed_in_editors_only()
    {
        var (factory, admin, detail) = await OpenAsync("hero");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        draft["lede"]!["en"] = "Preview sentence.";
        await admin.PutAsync("/api/admin/sections/hero/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });

        var preview = await admin.GetJsonAsync("/api/admin/preview/site?lang=en");
        preview.Section("hero").Str("lede").ShouldBe("Preview sentence.");
        preview["preview"]!.GetValue<bool>().ShouldBeTrue();

        using var visitor = factory.CreateQootClient();
        (await visitor.Http.GetAsync("/api/admin/preview/site?lang=en")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Discard_restores_the_published_version()
    {
        var (factory, admin, detail) = await OpenAsync("visit");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        draft["title"]!["en"] = "Temporary";
        await admin.PutAsync("/api/admin/sections/visit/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });

        var (status, body) = await admin.PostAsync("/api/admin/sections/visit/discard");
        status.ShouldBe(HttpStatusCode.OK);
        body!["draft"]!["title"]!["en"]!.GetValue<string>().ShouldBe("Visit us in Benghazi");
        body["section"]!["hasUnpublishedChanges"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task Hidden_sections_disappear_for_visitors_and_come_back()
    {
        var (factory, admin, _) = await OpenAsync("who");
        using var __ = factory;
        using var visitor = factory.CreateQootClient();

        (await admin.PutAsync("/api/admin/sections/who/visibility", new { visible = false })).Status.ShouldBe(HttpStatusCode.OK);
        var keys = (await visitor.SiteAsync()).AsObject()["sections"]!.AsArray().Select(s => s.Str("key")).ToList();
        keys.ShouldNotContain("who");

        await admin.PutAsync("/api/admin/sections/who/visibility", new { visible = true });
        (await visitor.SiteAsync())["sections"]!.AsArray().Select(s => s.Str("key")).ShouldContain("who");
    }

    [Fact]
    public async Task Header_and_hero_cannot_be_hidden()
    {
        var (factory, admin, _) = await OpenAsync("header");
        using var __ = factory;
        var (status, body) = await admin.PutAsync("/api/admin/sections/header/visibility", new { visible = false });
        status.ShouldBe(HttpStatusCode.BadRequest);
        body.Str("code").ShouldBe("section_cannot_hide");
        (await admin.PutAsync("/api/admin/sections/hero/visibility", new { visible = false })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sections_can_be_reordered_between_header_and_footer()
    {
        var (factory, admin, _) = await OpenAsync("header");
        using var __ = factory;
        var order = new[] { "hero", "news", "supplies", "how", "who", "faq", "contact", "visit" };
        var (status, _) = await admin.PutAsync("/api/admin/sections/order", new { keys = order });
        status.ShouldBe(HttpStatusCode.OK);

        using var visitor = factory.CreateQootClient();
        var keys = (await visitor.SiteAsync())["sections"]!.AsArray().Select(s => s.Str("key")).ToList();
        keys.ShouldBe(["header", .. order, "footer"]);

        // Every movable section must be listed exactly once.
        (await admin.PutAsync("/api/admin/sections/order", new { keys = order.Take(3) })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PutAsync("/api/admin/sections/order", new { keys = new[] { "header", "hero" } })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Saving_with_a_stale_version_is_refused_with_409()
    {
        var (factory, admin, detail) = await OpenAsync("faq");
        using var _ = factory;
        var stamp = detail["concurrencyStamp"]!.GetValue<Guid>();
        var draft = detail["draft"]!.CloneObject();
        (await admin.PutAsync("/api/admin/sections/faq/draft", new { data = draft, concurrencyStamp = stamp })).Status.ShouldBe(HttpStatusCode.OK);
        var (status, body) = await admin.PutAsync("/api/admin/sections/faq/draft", new { data = draft, concurrencyStamp = stamp });
        status.ShouldBe(HttpStatusCode.Conflict);
        body.Str("code").ShouldBe("concurrency_conflict");
    }

    [Fact]
    public async Task Unsafe_links_long_texts_and_missing_pictures_are_rejected_with_field_paths()
    {
        var (factory, admin, detail) = await OpenAsync("header");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        draft["links"]![0]!["target"] = "javascript:alert(1)";
        draft["ctaLabel"]!["en"] = new string('x', 41);
        var (status, body) = await admin.PutAsync("/api/admin/sections/header/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });
        status.ShouldBe(HttpStatusCode.BadRequest);
        body.Str("code").ShouldBe("validation_failed");
        var errors = body!["errors"]!.AsObject();
        errors.ContainsKey("links[0].target").ShouldBeTrue();
        errors.ContainsKey("ctaLabel.en").ShouldBeTrue();

        var hero = await admin.GetJsonAsync("/api/admin/sections/hero");
        var heroDraft = hero["draft"]!.CloneObject();
        heroDraft["slides"]![0]!["image"] = 999_999;
        var (heroStatus, heroBody) = await admin.PutAsync("/api/admin/sections/hero/draft", new { data = heroDraft, concurrencyStamp = hero["concurrencyStamp"]!.GetValue<Guid>() });
        heroStatus.ShouldBe(HttpStatusCode.BadRequest);
        heroBody!["errors"]!.AsObject().ContainsKey("slides[0].image").ShouldBeTrue();
    }

    [Fact]
    public async Task Html_typed_into_content_is_stored_as_plain_text()
    {
        var (factory, admin, detail) = await OpenAsync("faq");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        draft["items"]![0]!["answer"]!["en"] = "<script>alert('x')</script> Daily runs.";
        await admin.PutAsync("/api/admin/sections/faq/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });
        await admin.PostAsync("/api/admin/sections/faq/publish");

        using var visitor = factory.CreateQootClient();
        var answer = (await visitor.SiteAsync()).Section("faq")["items"]![0]!.Str("answer");
        // Kept verbatim as text: the website inserts it with textContent, so it can never run.
        answer.ShouldBe("<script>alert('x')</script> Daily runs.");
    }

    [Fact]
    public async Task List_items_can_be_hidden_added_and_removed()
    {
        var (factory, admin, detail) = await OpenAsync("faq");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        var items = draft["items"]!.AsArray();
        items[0]!["hidden"] = true;
        items.RemoveAt(1);
        items.Add(new JsonObject { ["question"] = new JsonObject { ["ar"] = "سؤال جديد؟", ["en"] = "A new question?" }, ["answer"] = new JsonObject { ["ar"] = "جواب.", ["en"] = "An answer." } });
        await admin.PutAsync("/api/admin/sections/faq/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });
        await admin.PostAsync("/api/admin/sections/faq/publish");

        using var visitor = factory.CreateQootClient();
        var published = (await visitor.SiteAsync()).Section("faq")["items"]!.AsArray();
        published.Count.ShouldBe(5); // 6 - 1 removed - 1 hidden + 1 new
        published.Last()!.Str("question").ShouldBe("A new question?");
        published.Last()!.Str("id").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Missing_translations_fall_back_to_the_other_language_and_are_reported()
    {
        var (factory, admin, detail) = await OpenAsync("faq");
        using var _ = factory;
        var draft = detail["draft"]!.CloneObject();
        draft["title"]!["en"] = "";
        var (_, saved) = await admin.PutAsync("/api/admin/sections/faq/draft", new { data = draft, concurrencyStamp = detail["concurrencyStamp"]!.GetValue<Guid>() });
        saved!["missingTranslations"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldContain("title.en");
        await admin.PostAsync("/api/admin/sections/faq/publish");

        using var visitor = factory.CreateQootClient();
        (await visitor.SiteAsync("en")).Section("faq").Str("title").ShouldBe("أسئلة يطرحها عملاؤنا");
    }
}
