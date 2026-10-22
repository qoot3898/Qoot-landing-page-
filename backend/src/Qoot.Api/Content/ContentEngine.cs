using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Content;

public sealed class ContentValidationContext
{
    public FieldErrors Errors { get; } = new();

    /// <summary>Paths of texts filled in one language only (shown as warnings, not errors).</summary>
    public List<string> MissingTranslations { get; } = [];

    public List<MediaRef> MediaRefs { get; } = [];
}

public sealed record MediaRef(int Id, string Path, bool AllowDrawings);

/// <summary>
/// Validates and normalises content JSON against a schema. Unknown keys are dropped, every text is
/// cleaned, links are checked against an allow-list and list items keep a stable id.
/// </summary>
public static partial class ContentValidator
{
    public static JsonObject Validate(IReadOnlyList<FieldDef> fields, JsonNode? input, ContentValidationContext ctx, string prefix = "")
    {
        var source = input as JsonObject;
        var result = new JsonObject();
        foreach (var field in fields)
        {
            var path = prefix.Length == 0 ? field.Key : $"{prefix}.{field.Key}";
            result[field.Key] = ValidateField(field, source?[field.Key], path, ctx);
        }

        return result;
    }

    private static JsonNode? ValidateField(FieldDef field, JsonNode? value, string path, ContentValidationContext ctx)
    {
        switch (field.Type)
        {
            case FieldType.Text or FieldType.TextArea when field.Localized:
                return ValidateLocalized(field, value, path, ctx);

            case FieldType.Text or FieldType.TextArea:
            {
                var text = SafeText.Clean(ReadString(value), field.Type == FieldType.TextArea);
                CheckText(field, text, path, ctx);
                return text;
            }

            case FieldType.Url:
            {
                var url = SafeText.Clean(ReadString(value));
                if (url.Length == 0)
                {
                    if (field.Required)
                    {
                        ctx.Errors.Add(path, "required");
                    }
                }
                else if (!SafeText.IsSafeLink(url))
                {
                    ctx.Errors.Add(path, "invalid_url");
                }

                return url;
            }

            case FieldType.Media:
            {
                var id = ReadInt(value);
                if (id is null or <= 0)
                {
                    if (field.Required)
                    {
                        ctx.Errors.Add(path, "required");
                    }

                    return null;
                }

                ctx.MediaRefs.Add(new MediaRef(id.Value, path, field.AllowDrawings));
                return id.Value;
            }

            case FieldType.Select or FieldType.Icon:
            {
                var choice = ReadString(value)?.Trim() ?? "";
                if (choice.Length == 0)
                {
                    if (field.Required)
                    {
                        ctx.Errors.Add(path, "required");
                    }

                    return "";
                }

                if (field.Options is not null && field.Options.All(o => o.Value != choice))
                {
                    ctx.Errors.Add(path, "invalid_option");
                    return "";
                }

                return choice;
            }

            case FieldType.MultiSelect:
            {
                var picked = new JsonArray();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (value is JsonArray array)
                {
                    foreach (var node in array)
                    {
                        var choice = ReadString(node)?.Trim() ?? "";
                        if (field.Options?.Any(o => o.Value == choice) == true && seen.Add(choice))
                        {
                            picked.Add(choice);
                        }
                        else if (choice.Length > 0)
                        {
                            ctx.Errors.Add(path, "invalid_option");
                        }
                    }
                }

                if (field.Required && picked.Count == 0)
                {
                    ctx.Errors.Add(path, "required");
                }

                return picked;
            }

            case FieldType.Bool:
                return value is JsonValue v && v.TryGetValue<bool>(out var b) && b;

            case FieldType.Number:
            {
                if (value is null || (value is JsonValue jv && jv.TryGetValue<string>(out var str) && string.IsNullOrWhiteSpace(str)))
                {
                    if (field.Required)
                    {
                        ctx.Errors.Add(path, "required");
                    }

                    return null;
                }

                var number = ReadInt(value);
                var min = field.Min ?? int.MinValue;
                var max = field.Max ?? int.MaxValue;
                if (number is null || number < min || number > max)
                {
                    ctx.Errors.Add(path, "invalid_number", field.Min ?? 0, field.Max ?? 0);
                    return null;
                }

                return number.Value;
            }

            case FieldType.Days:
            {
                var days = new SortedSet<int>();
                if (value is JsonArray array)
                {
                    foreach (var node in array)
                    {
                        var day = ReadInt(node);
                        if (day is >= 0 and <= 6)
                        {
                            days.Add(day.Value);
                        }
                        else
                        {
                            ctx.Errors.Add(path, "invalid_days");
                        }
                    }
                }
                else if (value is not null)
                {
                    ctx.Errors.Add(path, "invalid_days");
                }

                return new JsonArray(days.Select(d => (JsonNode)d).ToArray());
            }

            case FieldType.Time:
            {
                var time = ReadString(value)?.Trim() ?? "";
                if (time.Length == 0)
                {
                    if (field.Required)
                    {
                        ctx.Errors.Add(path, "required");
                    }

                    return "";
                }

                if (!TimeOfDay().IsMatch(time))
                {
                    ctx.Errors.Add(path, "invalid_time");
                    return "";
                }

                return time;
            }

            case FieldType.List:
                return ValidateList(field, value, path, ctx);

            default:
                return null;
        }
    }

    private static JsonObject ValidateLocalized(FieldDef field, JsonNode? value, string path, ContentValidationContext ctx)
    {
        var multiline = field.Type == FieldType.TextArea;
        string ar, en;
        if (value is JsonObject obj)
        {
            ar = SafeText.Clean(ReadString(obj["ar"]), multiline);
            en = SafeText.Clean(ReadString(obj["en"]), multiline);
        }
        else
        {
            // A bare string is accepted as Arabic, the site's root language.
            ar = SafeText.Clean(ReadString(value), multiline);
            en = "";
        }

        if (ar.Length > field.MaxLength)
        {
            ctx.Errors.Add(path + ".ar", "too_long", field.MaxLength);
        }

        if (en.Length > field.MaxLength)
        {
            ctx.Errors.Add(path + ".en", "too_long", field.MaxLength);
        }

        if (ar.Length == 0 && en.Length == 0)
        {
            if (field.Required)
            {
                ctx.Errors.Add(path + ".ar", "required");
            }
        }
        else if (ar.Length == 0)
        {
            ctx.MissingTranslations.Add(path + ".ar");
        }
        else if (en.Length == 0)
        {
            ctx.MissingTranslations.Add(path + ".en");
        }

        return new JsonObject { ["ar"] = ar, ["en"] = en };
    }

    private static void CheckText(FieldDef field, string text, string path, ContentValidationContext ctx)
    {
        if (text.Length == 0)
        {
            if (field.Required)
            {
                ctx.Errors.Add(path, "required");
            }

            return;
        }

        if (text.Length > field.MaxLength)
        {
            ctx.Errors.Add(path, "too_long", field.MaxLength);
            return;
        }

        switch (field.Format)
        {
            case TextFormat.Email when !SafeText.IsEmail(text):
                ctx.Errors.Add(path, "invalid_email");
                break;
            case TextFormat.Phone when !SafeText.IsPhone(text):
                ctx.Errors.Add(path, "invalid_phone");
                break;
            case TextFormat.AbsoluteUrl when !SafeText.IsAbsoluteHttpUrl(text):
                ctx.Errors.Add(path, "invalid_absolute_url");
                break;
        }
    }

    private static JsonArray ValidateList(FieldDef field, JsonNode? value, string path, ContentValidationContext ctx)
    {
        var result = new JsonArray();
        var items = value as JsonArray ?? [];
        if (items.Count > field.MaxItems)
        {
            ctx.Errors.Add(path, "too_many_items", field.MaxItems);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var node in items.Take(field.MaxItems))
        {
            var itemPath = $"{path}[{index}]";
            var validated = Validate(field.ItemFields ?? [], node, ctx, itemPath);
            var id = ReadString(node?["id"]);
            if (id is null || !ItemId().IsMatch(id) || !ids.Add(id))
            {
                id = Guid.NewGuid().ToString("N")[..12];
                ids.Add(id);
            }

            validated["id"] = id;
            validated["hidden"] = node?["hidden"] is JsonValue hv && hv.TryGetValue<bool>(out var hidden) && hidden;
            result.Add(validated);
            index++;
        }

        var visible = result.Count(n => n?["hidden"]?.GetValue<bool>() != true);
        if (visible < field.MinItems)
        {
            ctx.Errors.Add(path, "too_few_items", field.MinItems);
        }

        return result;
    }

    internal static string? ReadString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    internal static int? ReadInt(JsonNode? node)
    {
        if (node is not JsonValue v)
        {
            return null;
        }

        if (v.TryGetValue<int>(out var i))
        {
            return i;
        }

        if (v.TryGetValue<long>(out var l) && l is >= int.MinValue and <= int.MaxValue)
        {
            return (int)l;
        }

        if (v.TryGetValue<double>(out var d) && Math.Abs(d % 1) < double.Epsilon && d is >= int.MinValue and <= int.MaxValue)
        {
            return (int)d;
        }

        return v.TryGetValue<string>(out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    [GeneratedRegex(@"^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    private static partial Regex TimeOfDay();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex ItemId();
}

/// <summary>Walks stored content to find every media reference (for usage checks and payload building).</summary>
public static class MediaRefCollector
{
    public static void Collect(IReadOnlyList<FieldDef> fields, JsonNode? data, ICollection<int> ids)
    {
        if (data is not JsonObject obj)
        {
            return;
        }

        foreach (var field in fields)
        {
            var value = obj[field.Key];
            switch (field.Type)
            {
                case FieldType.Media when ContentValidator.ReadInt(value) is { } id:
                    ids.Add(id);
                    break;
                case FieldType.List when value is JsonArray items:
                    foreach (var item in items)
                    {
                        Collect(field.ItemFields ?? [], item, ids);
                    }

                    break;
            }
        }
    }
}
