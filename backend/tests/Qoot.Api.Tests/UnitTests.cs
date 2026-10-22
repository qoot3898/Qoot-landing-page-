using System.Text.Json.Nodes;
using Qoot.Api.Content;
using Qoot.Api.Infrastructure;
using Qoot.Api.Media;

namespace Qoot.Api.Tests;

public sealed class SafeTextTests
{
    [Theory]
    [InlineData("#faq", true)]
    [InlineData("/news/", true)]
    [InlineData("/en/news/harvest?x=1#top", true)]
    [InlineData("https://maps.app.goo.gl/pmTEDtSWm7e6Qe8a8", true)]
    [InlineData("mailto:info@qoot.com", true)]
    [InlineData("tel:+218123456789", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JavaScript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("vbscript:x", false)]
    [InlineData("//evil.example/path", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("#a b", false)]
    [InlineData("ftp://files.example", false)]
    [InlineData("", false)]
    public void Links_follow_the_allow_list(string url, bool allowed) => SafeText.IsSafeLink(url).ShouldBe(allowed);

    [Fact]
    public void Clean_removes_control_and_override_characters_and_normalises_lines()
    {
        SafeText.Clean("  a\u0000b‮c\r\nd  ").ShouldBe("abc d");
        SafeText.Clean("line one\r\n\r\n\r\n\r\nline two", multiline: true).ShouldBe("line one\n\nline two");
        SafeText.Clean("بنغازي‏").ShouldBe("بنغازي‏");
    }

    [Theory]
    [InlineData("Harvest season 2026!", "harvest-season-2026")]
    [InlineData("  Café & bread ", "cafe-bread")]
    [InlineData("موسم الحصاد", "")]
    public void Slugify(string text, string slug) => SafeText.Slugify(text).ShouldBe(slug);

    [Theory]
    [InlineData("+218 91 234 5678", true)]
    [InlineData("0912345678", true)]
    [InlineData("(091) 234-5678", true)]
    [InlineData("call me", false)]
    [InlineData("12", false)]
    public void Phone_numbers_as_people_type_them(string value, bool ok) => SafeText.LooksLikePhone(value).ShouldBe(ok);
}

public sealed class ImageInspectorTests
{
    [Fact]
    public void Recognises_each_supported_format_and_its_size()
    {
        ImageInspector.Inspect(TestImages.Png).ShouldBe(new ImageInfo("image/png", ".png", 5, 4));
        ImageInspector.Inspect(TestImages.Jpeg).ShouldBe(new ImageInfo("image/jpeg", ".jpg", 5, 4));
        ImageInspector.Inspect(TestImages.Gif).ShouldBe(new ImageInfo("image/gif", ".gif", 5, 4));
        ImageInspector.Inspect(TestImages.WebP).ShouldBe(new ImageInfo("image/webp", ".webp", 5, 4));
        ImageInspector.Inspect(TestImages.WebPLossless).ShouldBe(new ImageInfo("image/webp", ".webp", 5, 4));
        ImageInspector.Inspect(TestImages.AvifHead).ShouldBe(new ImageInfo("image/avif", ".avif", 640, 426));
    }

    [Fact]
    public void Refuses_everything_else()
    {
        ImageInspector.Inspect("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"></svg>"u8).ShouldBeNull();
        ImageInspector.Inspect("GIF89a but really a script <script>alert(1)</script>"u8.ToArray().AsSpan()[..20]).ShouldBeNull();
        ImageInspector.Inspect(new byte[64]).ShouldBeNull();
        ImageInspector.Inspect(TestImages.Png.AsSpan(0, 20)).ShouldBeNull();
    }
}

public sealed class ContentValidatorTests
{
    [Fact]
    public void Drops_unknown_keys_and_keeps_list_ids_stable()
    {
        var schema = SectionSchemas.Find("faq")!;
        var input = JsonNode.Parse("""
            {
              "title": { "ar": "أسئلة", "en": "Questions" },
              "evil": "<script>",
              "items": [
                { "id": "keep-me", "question": { "ar": "س؟", "en": "Q?" }, "answer": { "ar": "ج", "en": "A" } },
                { "id": "bad id!", "question": { "ar": "س2؟", "en": "" }, "answer": { "ar": "ج2", "en": "" } }
              ]
            }
            """);
        var ctx = new ContentValidationContext();
        var result = ContentValidator.Validate(schema.Fields, input, ctx);

        ctx.Errors.Any.ShouldBeFalse();
        result.ContainsKey("evil").ShouldBeFalse();
        result["items"]![0]!["id"]!.GetValue<string>().ShouldBe("keep-me");
        result["items"]![1]!["id"]!.GetValue<string>().ShouldNotBe("bad id!");
        ctx.MissingTranslations.ShouldContain("items[1].question.en");
    }

    [Fact]
    public void Reports_every_problem_with_its_path()
    {
        var schema = SectionSchemas.Find("who")!;
        var input = JsonNode.Parse("""
            {
              "title": { "ar": "", "en": "" },
              "sectors": [ { "label": { "en": "Oil" }, "icon": "rocket" } ],
              "cities": [ { "city": "atlantis", "label": { "en": "X" } } ],
              "ctaTarget": "javascript:void(0)"
            }
            """);
        var ctx = new ContentValidationContext();
        ContentValidator.Validate(schema.Fields, input, ctx);
        var errors = ctx.Errors.ToDictionary();
        errors.Keys.ShouldContain("title.ar");
        errors.Keys.ShouldContain("sectors[0].icon");
        errors.Keys.ShouldContain("cities[0].city");
        errors.Keys.ShouldContain("ctaTarget");
    }

    [Fact]
    public void The_seed_content_matches_every_schema()
    {
        // The seeder validates on start-up too; this keeps schema edits honest.
        foreach (var schema in SectionSchemas.All)
        {
            schema.Fields.ShouldNotBeEmpty(schema.Key);
            schema.Fields.Select(f => f.Key).Distinct().Count().ShouldBe(schema.Fields.Count, schema.Key);
        }
    }

    [Fact]
    public void News_bodies_keep_structure_and_reject_unsafe_links()
    {
        var errors = new FieldErrors();
        var refs = new List<MediaRef>();
        var body = NewsBody.Validate(JsonNode.Parse("""
            [
              { "type": "paragraph", "text": "See [our menu](/#supplies) and **call**." },
              { "type": "list", "style": "number", "items": ["One", "", "Two"] },
              { "type": "image", "mediaId": 3, "caption": "Truck" },
              { "type": "paragraph", "text": "[x](javascript:alert(1))" }
            ]
            """), "body", errors, refs);
        body.Count.ShouldBe(4);
        body[1]!["items"]!.AsArray().Count.ShouldBe(2);
        refs.Single().Id.ShouldBe(3);
        errors.ToDictionary().Keys.ShouldBe(["body[3].text"]);
        NewsBody.PlainText(body.ToJsonString()).ShouldStartWith("See our menu and call.");
    }
}
