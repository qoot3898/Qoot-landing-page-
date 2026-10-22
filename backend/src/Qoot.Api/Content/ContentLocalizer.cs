using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qoot.Api.Domain;
using Qoot.Api.Media;

namespace Qoot.Api.Content;

/// <summary>
/// Turns stored bilingual content into what one visitor needs: texts in their language (falling back
/// to the other language when a translation is missing), hidden list items removed, and media ids
/// replaced by ready-to-use picture descriptions.
/// </summary>
public sealed class ContentLocalizer(string lang, IReadOnlyDictionary<int, MediaAsset> media, MediaUrls urls)
{
    private readonly string _fallback = lang == "ar" ? "en" : "ar";

    /// <summary>The visitor's language ("ar" or "en").</summary>
    public string Language { get; } = lang;

    public JsonObject Localize(IReadOnlyList<FieldDef> fields, JsonNode? data)
    {
        var source = data as JsonObject;
        var result = new JsonObject();
        foreach (var field in fields)
        {
            if (field.Private)
            {
                continue;
            }

            result[field.Key] = LocalizeField(field, source?[field.Key]);
        }

        return result;
    }

    private JsonNode? LocalizeField(FieldDef field, JsonNode? value)
    {
        switch (field.Type)
        {
            case FieldType.Text or FieldType.TextArea when field.Localized:
                return Text(value);

            case FieldType.Media:
                return ContentValidator.ReadInt(value) is { } id && media.TryGetValue(id, out var asset) ? Picture(asset) : null;

            case FieldType.List:
            {
                var list = new JsonArray();
                if (value is JsonArray items)
                {
                    foreach (var item in items)
                    {
                        if (item?["hidden"] is JsonValue hv && hv.TryGetValue<bool>(out var hidden) && hidden)
                        {
                            continue;
                        }

                        var localized = Localize(field.ItemFields ?? [], item);
                        localized["id"] = ContentValidator.ReadString(item?["id"]) ?? "";
                        list.Add(localized);
                    }
                }

                return list;
            }

            default:
                return value?.DeepClone();
        }
    }

    public string Text(JsonNode? value)
    {
        if (value is not JsonObject obj)
        {
            return ContentValidator.ReadString(value) ?? "";
        }

        var text = ContentValidator.ReadString(obj[lang]);
        return string.IsNullOrEmpty(text) ? ContentValidator.ReadString(obj[_fallback]) ?? "" : text;
    }

    public string Text(string ar, string en)
    {
        var (primary, other) = lang == "ar" ? (ar, en) : (en, ar);
        return string.IsNullOrEmpty(primary) ? other : primary;
    }

    public JsonObject Picture(MediaAsset asset) => urls.Describe(asset, Text(asset.AltAr, asset.AltEn));

    public JsonObject? Picture(int? id) => id is { } value && media.TryGetValue(value, out var asset) ? Picture(asset) : null;
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// What the API writes: Arabic stays readable (half the bytes of \uXXXX escapes) while the
    /// HTML-sensitive characters &lt; &gt; &amp; ' " + ` are still escaped.
    /// </summary>
    public static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);

    public static readonly JsonSerializerOptions Output = new(JsonSerializerDefaults.Web) { Encoder = Encoder };

    public static JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
