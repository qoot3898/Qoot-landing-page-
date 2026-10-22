using System.Net;
using System.Text.Json.Nodes;
using Qoot.Api.Tests.Support;

namespace Qoot.Api.Tests;

public sealed class NewsTests
{
    private static object Article(string titleEn, string titleAr = "", string? slug = null, int priority = 0, bool featured = false, int? categoryId = null, int? imageId = null, object? bodyEn = null) => new
    {
        slug,
        titleEn,
        titleAr,
        summaryEn = "Summary of " + titleEn,
        summaryAr = titleAr.Length > 0 ? "ملخص " + titleAr : "",
        bodyEn = bodyEn ?? new object[] { new { type = "paragraph", text = "First paragraph with **bold** and a [link](https://www.qoot.com/)." }, new { type = "heading", text = "A heading", level = 2 } },
        bodyAr = Array.Empty<object>(),
        featuredImageId = imageId,
        categoryId,
        isFeatured = featured,
        priority,
    };

    private static async Task<int> CreateAsync(QootClient admin, object article)
    {
        var (status, body) = await admin.PostAsync("/api/admin/news", article);
        status.ShouldBe(HttpStatusCode.Created, body?.ToJsonString());
        return body!["id"]!.GetValue<int>();
    }

    [Fact]
    public async Task Drafts_are_invisible_until_published_and_unpublishing_hides_them_again()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var id = await CreateAsync(admin, Article("Harvest season", "موسم الحصاد", slug: "harvest-season"));

        (await visitor.GetJsonAsync("/api/public/news?lang=en"))["total"]!.GetValue<int>().ShouldBe(0);
        await visitor.GetJsonAsync("/api/public/news/harvest-season?lang=en", HttpStatusCode.NotFound);
        (await visitor.Http.GetStringAsync("/sitemap.xml")).ShouldNotContain("harvest-season");

        var (published, detail) = await admin.PostAsync($"/api/admin/news/{id}/publish", new { });
        published.ShouldBe(HttpStatusCode.OK);
        detail.Str("status").ShouldBe("published");

        var list = await visitor.GetJsonAsync("/api/public/news?lang=en");
        list["total"]!.GetValue<int>().ShouldBe(1);
        var article = await visitor.GetJsonAsync("/api/public/news/harvest-season?lang=ar");
        article.Str("title").ShouldBe("موسم الحصاد");
        article["seo"]!.Str("canonical").ShouldBe("https://www.qoot.com/news/harvest-season");
        (await visitor.Http.GetStringAsync("/sitemap.xml")).ShouldContain("/en/news/harvest-season");

        await admin.PostAsync($"/api/admin/news/{id}/unpublish");
        await visitor.GetJsonAsync("/api/public/news/harvest-season?lang=en", HttpStatusCode.NotFound);
        (await visitor.GetJsonAsync("/api/public/news?lang=en"))["total"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public async Task A_card_without_a_summary_shows_the_start_of_the_article()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var (status, body) = await admin.PostAsync("/api/admin/news", new
        {
            slug = "cold-store",
            titleEn = "A new cold store",
            titleAr = "",
            summaryEn = "",
            summaryAr = "",
            bodyEn = new object[] { new { type = "paragraph", text = "We opened a **new** cold store in Benghazi this week." } },
            bodyAr = Array.Empty<object>(),
        });
        status.ShouldBe(HttpStatusCode.Created, body?.ToJsonString());
        await admin.PostAsync($"/api/admin/news/{body!["id"]!.GetValue<int>()}/publish", new { });

        var card = (await visitor.GetJsonAsync("/api/public/news?lang=en"))["items"]!.AsArray().Single()!;
        card.Str("summary").ShouldBe("We opened a new cold store in Benghazi this week.");
        // The article itself has no lede: its text is not shown twice.
        (await visitor.GetJsonAsync("/api/public/news/cold-store?lang=en")).Str("summary").ShouldBe("");
    }

    [Fact]
    public async Task A_card_carries_the_whole_story_so_the_website_can_open_it_in_place()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var id = await CreateAsync(admin, Article("Winter citrus", slug: "winter-citrus", bodyEn: new object[]
        {
            new { type = "paragraph", text = "Oranges arrived from **Al Marj**." },
            new { type = "heading", text = "Sizes", level = 2 },
            new { type = "list", style = "number", items = new[] { "Small", "Large" } },
            new { type = "quote", text = "Sweet this year.", cite = "Our buyer" },
        }));
        await admin.PostAsync($"/api/admin/news/{id}/publish", new { });

        var card = (await visitor.GetJsonAsync("/api/public/news?lang=en"))["items"]!.AsArray().Single()!;
        card.Str("text").ShouldBe("Summary of Winter citrus\n\nOranges arrived from **Al Marj**.\n\nSizes\n\n1. Small\n2. Large\n\nSweet this year.");
        // The article page has the blocks; it does not get the flat text as well.
        (await visitor.GetJsonAsync("/api/public/news/winter-citrus?lang=en"))["text"].ShouldBeNull();
    }

    [Fact]
    public async Task Scheduled_articles_appear_only_when_their_time_comes()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var id = await CreateAsync(admin, Article("Opening the Tobruk depot", slug: "tobruk-depot"));
        var when = factory.Clock.GetUtcNow().AddDays(2).UtcDateTime;
        var (_, detail) = await admin.PostAsync($"/api/admin/news/{id}/publish", new { publishAt = when });
        detail.Str("status").ShouldBe("scheduled");

        await visitor.GetJsonAsync("/api/public/news/tobruk-depot?lang=en", HttpStatusCode.NotFound);
        factory.Clock.Advance(TimeSpan.FromDays(3));
        (await visitor.GetJsonAsync("/api/public/news/tobruk-depot?lang=en")).Str("title").ShouldBe("Opening the Tobruk depot");
    }

    [Fact]
    public async Task Newest_first_with_priority_and_featured_controls_and_pagination()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        for (var i = 1; i <= 5; i++)
        {
            var id = await CreateAsync(admin, Article($"Story {i}", slug: $"story-{i}", featured: i == 2));
            await admin.PostAsync($"/api/admin/news/{id}/publish", new { publishAt = factory.Clock.GetUtcNow().AddMinutes(-60 + i).UtcDateTime });
        }

        var pinned = await CreateAsync(admin, Article("Pinned notice", slug: "pinned-notice", priority: 10));
        await admin.PostAsync($"/api/admin/news/{pinned}/publish", new { publishAt = factory.Clock.GetUtcNow().AddDays(-30).UtcDateTime });

        var page1 = await visitor.GetJsonAsync("/api/public/news?lang=en&page=1&pageSize=3");
        page1["items"]!.AsArray().Select(a => a.Str("slug")).ShouldBe(["pinned-notice", "story-5", "story-4"]);
        page1["totalPages"]!.GetValue<int>().ShouldBe(2);
        var page2 = await visitor.GetJsonAsync("/api/public/news?lang=en&page=2&pageSize=3");
        page2["items"]!.AsArray().Select(a => a.Str("slug")).ShouldBe(["story-3", "story-2", "story-1"]);
        page2["items"]![1]!["featured"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task Categories_filter_the_public_list()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var (status, category) = await admin.PostAsync("/api/admin/news-categories", new { nameEn = "Contracts", nameAr = "العقود", sortOrder = 1 });
        status.ShouldBe(HttpStatusCode.OK, category?.ToJsonString());
        var categoryId = category!["id"]!.GetValue<int>();
        category.Str("slug").ShouldBe("contracts");

        var a = await CreateAsync(admin, Article("New camp contract", slug: "camp-contract", categoryId: categoryId));
        var b = await CreateAsync(admin, Article("Other news", slug: "other-news"));
        await admin.PostAsync($"/api/admin/news/{a}/publish", new { });
        await admin.PostAsync($"/api/admin/news/{b}/publish", new { });

        var filtered = await visitor.GetJsonAsync("/api/public/news?lang=ar&category=contracts");
        filtered["items"]!.AsArray().Single()!["category"]!.Str("name").ShouldBe("العقود");
        (await visitor.GetJsonAsync("/api/public/news/categories?lang=en"))["items"]!.AsArray().Single()!.Str("name").ShouldBe("Contracts");

        (await admin.DeleteAsync($"/api/admin/news-categories/{categoryId}")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await visitor.GetJsonAsync("/api/public/news/camp-contract?lang=en"))["category"].ShouldBeNull();
    }

    [Fact]
    public async Task Missing_translation_falls_back_and_says_which_language_the_body_is_in()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var id = await CreateAsync(admin, Article("English only", slug: "english-only"));
        await admin.PostAsync($"/api/admin/news/{id}/publish", new { });

        var arabic = await visitor.GetJsonAsync("/api/public/news/english-only?lang=ar");
        arabic.Str("title").ShouldBe("English only");
        arabic.Str("contentLang").ShouldBe("en");
        arabic["body"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Bodies_reject_unsafe_links_and_unknown_blocks()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (status, body) = await admin.PostAsync("/api/admin/news", Article("Bad link", bodyEn: new object[]
        {
            new { type = "paragraph", text = "Click [here](javascript:alert(1))" },
            new { type = "html", text = "<b>x</b>" },
        }));
        status.ShouldBe(HttpStatusCode.BadRequest);
        var errors = body!["errors"]!.AsObject();
        errors.ContainsKey("bodyEn[0].text").ShouldBeTrue();
        errors.ContainsKey("bodyEn[1]").ShouldBeTrue();
    }

    [Fact]
    public async Task Slugs_are_unique_and_generated_when_left_empty()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        await CreateAsync(admin, Article("Same title"));
        var (_, second) = await admin.PostAsync("/api/admin/news", Article("Same title"));
        second.Str("slug").ShouldBe("same-title-2");

        var (status, body) = await admin.PostAsync("/api/admin/news", Article("Another", slug: "same-title"));
        status.ShouldBe(HttpStatusCode.BadRequest);
        body!["errors"]!["slug"].ShouldNotBeNull();
        (await admin.PostAsync("/api/admin/news", Article("Bad slug", slug: "Not A Slug!"))).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_article_needs_a_title()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var (status, body) = await admin.PostAsync("/api/admin/news", Article(""));
        status.ShouldBe(HttpStatusCode.BadRequest);
        body!["errors"]!["titleAr"].ShouldNotBeNull();
    }

    [Fact]
    public async Task Deleted_articles_are_gone_everywhere()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        using var visitor = factory.CreateQootClient();
        var id = await CreateAsync(admin, Article("To delete", slug: "to-delete"));
        await admin.PostAsync($"/api/admin/news/{id}/publish", new { });
        (await admin.DeleteAsync($"/api/admin/news/{id}")).Status.ShouldBe(HttpStatusCode.NoContent);
        await visitor.GetJsonAsync("/api/public/news/to-delete?lang=en", HttpStatusCode.NotFound);
        await admin.GetJsonAsync($"/api/admin/news/{id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_list_filters_by_status_and_search()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var draft = await CreateAsync(admin, Article("Draft about bread", slug: "draft-bread"));
        var live = await CreateAsync(admin, Article("Live about fish", slug: "live-fish"));
        await admin.PostAsync($"/api/admin/news/{live}/publish", new { });

        (await admin.GetJsonAsync("/api/admin/news?status=draft"))["items"]!.AsArray().Single()!["id"]!.GetValue<int>().ShouldBe(draft);
        (await admin.GetJsonAsync("/api/admin/news?status=published"))["items"]!.AsArray().Single()!["id"]!.GetValue<int>().ShouldBe(live);
        (await admin.GetJsonAsync("/api/admin/news?search=fish"))["items"]!.AsArray().Single()!.Str("slug").ShouldBe("live-fish");
    }

    [Fact]
    public async Task Draft_preview_is_available_to_editors()
    {
        using var factory = new QootFactory();
        using var admin = await factory.SignInAsync();
        var id = await CreateAsync(admin, Article("Preview me", slug: "preview-me"));
        var preview = await admin.GetJsonAsync($"/api/admin/preview/news/{id}?lang=en");
        preview.Str("title").ShouldBe("Preview me");
        preview["preview"]!.GetValue<bool>().ShouldBeTrue();
        preview.Str("status").ShouldBe("draft");
    }
}
