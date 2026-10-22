using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Content;

/// <summary>
/// Article bodies are structured blocks, never HTML:
/// paragraph, heading (level 2 or 3), list (bullet or number), quote and image.
/// Texts may use two inline marks only: **bold** and [label](link). Links are checked against the
/// same allow-list as everywhere else, and the website builds the DOM from these blocks with
/// textContent, so nothing typed into an article can run as code.
/// </summary>
public static partial class NewsBody
{
    public const int MaxBlocks = 120;
    public const int MaxText = 4000;
    public const int MaxListItems = 40;

    public static JsonArray Validate(JsonNode? input, string path, FieldErrors errors, List<MediaRef> mediaRefs)
    {
        var result = new JsonArray();
        if (input is null)
        {
            return result;
        }

        if (input is not JsonArray blocks)
        {
            errors.Add(path, "invalid_block");
            return result;
        }

        if (blocks.Count > MaxBlocks)
        {
            errors.Add(path, "too_many_items", MaxBlocks);
        }

        var index = 0;
        foreach (var node in blocks.Take(MaxBlocks))
        {
            var blockPath = $"{path}[{index++}]";
            var type = ContentValidator.ReadString(node?["type"]);
            switch (type)
            {
                case "paragraph":
                {
                    var text = InlineText(node?["text"], blockPath + ".text", errors, multiline: true);
                    if (text.Length > 0)
                    {
                        result.Add(new JsonObject { ["type"] = "paragraph", ["text"] = text });
                    }

                    break;
                }

                case "heading":
                {
                    var text = SafeText.Clean(ContentValidator.ReadString(node?["text"]));
                    if (text.Length > 200)
                    {
                        errors.Add(blockPath + ".text", "too_long", 200);
                    }

                    var level = ContentValidator.ReadInt(node?["level"]) == 3 ? 3 : 2;
                    if (text.Length > 0)
                    {
                        result.Add(new JsonObject { ["type"] = "heading", ["text"] = text, ["level"] = level });
                    }

                    break;
                }

                case "list":
                {
                    var style = ContentValidator.ReadString(node?["style"]) == "number" ? "number" : "bullet";
                    var items = new JsonArray();
                    if (node?["items"] is JsonArray raw)
                    {
                        if (raw.Count > MaxListItems)
                        {
                            errors.Add(blockPath + ".items", "too_many_items", MaxListItems);
                        }

                        var i = 0;
                        foreach (var item in raw.Take(MaxListItems))
                        {
                            var text = InlineText(item, $"{blockPath}.items[{i++}]", errors, multiline: false);
                            if (text.Length > 0)
                            {
                                items.Add(text);
                            }
                        }
                    }

                    if (items.Count > 0)
                    {
                        result.Add(new JsonObject { ["type"] = "list", ["style"] = style, ["items"] = items });
                    }

                    break;
                }

                case "quote":
                {
                    var text = InlineText(node?["text"], blockPath + ".text", errors, multiline: true);
                    var cite = SafeText.Clean(ContentValidator.ReadString(node?["cite"]));
                    if (cite.Length > 160)
                    {
                        errors.Add(blockPath + ".cite", "too_long", 160);
                    }

                    if (text.Length > 0)
                    {
                        result.Add(new JsonObject { ["type"] = "quote", ["text"] = text, ["cite"] = cite });
                    }

                    break;
                }

                case "image":
                {
                    var id = ContentValidator.ReadInt(node?["mediaId"]);
                    var caption = SafeText.Clean(ContentValidator.ReadString(node?["caption"]));
                    if (caption.Length > 300)
                    {
                        errors.Add(blockPath + ".caption", "too_long", 300);
                    }

                    if (id is > 0)
                    {
                        mediaRefs.Add(new MediaRef(id.Value, blockPath + ".mediaId", AllowDrawings: false));
                        result.Add(new JsonObject { ["type"] = "image", ["mediaId"] = id.Value, ["caption"] = caption });
                    }
                    else
                    {
                        errors.Add(blockPath + ".mediaId", "required");
                    }

                    break;
                }

                default:
                    errors.Add(blockPath, "invalid_block");
                    break;
            }
        }

        return result;
    }

    /// <summary>Every media id used by a stored body.</summary>
    public static IEnumerable<int> MediaIds(string? json)
    {
        if (JsonDefaults.Parse(json) is not JsonArray blocks)
        {
            yield break;
        }

        foreach (var block in blocks)
        {
            if (ContentValidator.ReadString(block?["type"]) == "image" && ContentValidator.ReadInt(block?["mediaId"]) is { } id)
            {
                yield return id;
            }
        }
    }

    /// <summary>Plain text of a body (marks removed), used for search snippets and meta descriptions.</summary>
    public static string PlainText(string? json, int max = 300)
    {
        if (JsonDefaults.Parse(json) is not JsonArray blocks)
        {
            return "";
        }

        var text = string.Join(' ', blocks.Select(b => ContentValidator.ReadString(b?["text"]) ?? "").Where(t => t.Length > 0));
        text = Link().Replace(text, "$1").Replace("**", "", StringComparison.Ordinal);
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    /// <summary>
    /// The words of a body for a news card on the website: paragraphs, headings, quotes and list
    /// items in order, one paragraph per block separated by a blank line, list items one per line
    /// ("• " or "1. "). The two inline marks are kept (the website renders them as it does in the
    /// article); pictures are left out. Ends after the paragraph that passes <paramref name="max"/>
    /// characters, with "…" when something was left out.
    /// </summary>
    public static string CardText(string? json, int max = 6000)
    {
        if (JsonDefaults.Parse(json) is not JsonArray blocks)
        {
            return "";
        }

        var parts = new List<string>();
        var length = 0;
        foreach (var block in blocks)
        {
            var type = ContentValidator.ReadString(block?["type"]);
            var part = "";
            if (type is "paragraph" or "heading" or "quote")
            {
                part = (ContentValidator.ReadString(block?["text"]) ?? "").Trim();
            }
            else if (type == "list" && block?["items"] is JsonArray items)
            {
                var numbered = ContentValidator.ReadString(block?["style"]) == "number";
                var lines = new List<string>();
                foreach (var item in items)
                {
                    var text = (ContentValidator.ReadString(item) ?? "").Trim();
                    if (text.Length > 0)
                    {
                        lines.Add((numbered ? $"{lines.Count + 1}. " : "• ") + text);
                    }
                }

                part = string.Join('\n', lines);
            }

            if (part.Length == 0)
            {
                continue;
            }

            if (length >= max)
            {
                parts.Add("…");
                break;
            }

            parts.Add(part);
            length += part.Length;
        }

        return string.Join("\n\n", parts);
    }

    public static bool HasContent(string? json) => JsonDefaults.Parse(json) is JsonArray { Count: > 0 };

    private static string InlineText(JsonNode? node, string path, FieldErrors errors, bool multiline)
    {
        var text = SafeText.Clean(ContentValidator.ReadString(node), multiline);
        if (text.Length > MaxText)
        {
            errors.Add(path, "too_long", MaxText);
        }

        foreach (Match m in Link().Matches(text))
        {
            if (!SafeText.IsSafeLink(m.Groups[2].Value))
            {
                errors.Add(path, "invalid_link_in_text");
            }
        }

        return text;
    }

    [GeneratedRegex(@"\[([^\]\n]{1,200})\]\(([^)\s]{1,500})\)")]
    private static partial Regex Link();
}
