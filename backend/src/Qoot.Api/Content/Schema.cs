using System.Text.Json.Serialization;

namespace Qoot.Api.Content;

[JsonConverter(typeof(JsonStringEnumConverter<FieldType>))]
public enum FieldType
{
    /// <summary>One line of text.</summary>
    Text,

    /// <summary>A paragraph of plain text (line breaks kept, no markup).</summary>
    TextArea,

    /// <summary>A link target: #section, /path, https://..., mailto: or tel:.</summary>
    Url,

    /// <summary>A reference to an item in the media library.</summary>
    Media,

    Select,
    MultiSelect,

    /// <summary>A choice from the site's own icon set (rendered with a preview in the dashboard).</summary>
    Icon,

    Bool,
    Number,

    /// <summary>Repeated items, each with its own fields; items can be added, removed, reordered and hidden.</summary>
    List,

    /// <summary>Days of the week, 0 = Sunday ... 6 = Saturday.</summary>
    Days,

    /// <summary>24-hour time, HH:mm.</summary>
    Time,
}

[JsonConverter(typeof(JsonStringEnumConverter<TextFormat>))]
public enum TextFormat
{
    None,
    Email,
    Phone,
    AbsoluteUrl,
}

public sealed record Bilingual(string Ar, string En);

public sealed record OptionDef(string Value, Bilingual Label);

public sealed record GroupDef(string Key, Bilingual Label);

/// <summary>
/// Describes one editable field. The same definition drives server-side validation, the public
/// payload (localisation and media resolution) and the dashboard form.
/// </summary>
public sealed record FieldDef
{
    public required string Key { get; init; }
    public required FieldType Type { get; init; }
    public required Bilingual Label { get; init; }
    public Bilingual? Help { get; init; }

    /// <summary>Stored once per language ({ "ar": "...", "en": "..." }).</summary>
    public bool Localized { get; init; }

    public bool Required { get; init; }
    public int MaxLength { get; init; } = 200;
    public TextFormat Format { get; init; } = TextFormat.None;
    public IReadOnlyList<OptionDef>? Options { get; init; }
    public IReadOnlyList<FieldDef>? ItemFields { get; init; }

    /// <summary>For lists: which item field names the item in the dashboard.</summary>
    public string? ItemLabel { get; init; }

    public int MinItems { get; init; }
    public int MaxItems { get; init; } = 50;
    public int? Min { get; init; }
    public int? Max { get; init; }
    public string? Group { get; init; }

    /// <summary>Never sent to the public site (for example the notification address).</summary>
    public bool Private { get; init; }

    /// <summary>For media fields: also allow SVG drawings from the brand set.</summary>
    public bool AllowDrawings { get; init; }
}

public sealed record SectionSchema
{
    public required string Key { get; init; }
    public required Bilingual Name { get; init; }
    public required Bilingual Description { get; init; }

    /// <summary>The element id visitors land on (#supplies ...). Null when the section is not a link target.</summary>
    public string? Anchor { get; init; }

    /// <summary>Header and footer stay at the top and bottom of the page.</summary>
    public bool Pinned { get; init; }

    public bool CanHide { get; init; } = true;
    public IReadOnlyList<GroupDef> Groups { get; init; } = [];
    public required IReadOnlyList<FieldDef> Fields { get; init; }
}

/// <summary>Compact helpers for declaring fields.</summary>
internal static class F
{
    public static FieldDef Text(string key, string en, string ar, int max = 120, bool required = false, string? group = null) =>
        new() { Key = key, Type = FieldType.Text, Label = new(ar, en), Localized = true, MaxLength = max, Required = required, Group = group };

    public static FieldDef Area(string key, string en, string ar, int max = 600, bool required = false, string? group = null) =>
        new() { Key = key, Type = FieldType.TextArea, Label = new(ar, en), Localized = true, MaxLength = max, Required = required, Group = group };

    public static FieldDef Plain(string key, string en, string ar, int max = 120, TextFormat format = TextFormat.None, bool required = false, string? group = null) =>
        new() { Key = key, Type = FieldType.Text, Label = new(ar, en), Localized = false, MaxLength = max, Format = format, Required = required, Group = group };

    public static FieldDef Link(string key, string en, string ar, bool required = false, string? group = null) =>
        new()
        {
            Key = key, Type = FieldType.Url, Label = new(ar, en), MaxLength = 500, Required = required, Group = group,
            Help = new("‎#قسم داخل الصفحة، أو ‎/news/‎، أو رابط كامل يبدأ بـ https://، أو mailto: أو tel:", "A section on the page (#faq), a page (/news/), a full https:// address, mailto: or tel:"),
        };

    public static FieldDef Media(string key, string en, string ar, bool required = false, bool drawings = false, string? group = null) =>
        new() { Key = key, Type = FieldType.Media, Label = new(ar, en), Required = required, AllowDrawings = drawings, Group = group };

    public static FieldDef Select(string key, string en, string ar, IReadOnlyList<OptionDef> options, bool required = false, string? group = null) =>
        new() { Key = key, Type = FieldType.Select, Label = new(ar, en), Options = options, Required = required, Group = group };

    public static FieldDef Icon(string key, string en, string ar, IReadOnlyList<OptionDef> options, bool required = true) =>
        new() { Key = key, Type = FieldType.Icon, Label = new(ar, en), Options = options, Required = required };

    public static FieldDef Bool(string key, string en, string ar, string? group = null) =>
        new() { Key = key, Type = FieldType.Bool, Label = new(ar, en), Group = group };

    public static FieldDef Number(string key, string en, string ar, int min, int max, string? group = null) =>
        new() { Key = key, Type = FieldType.Number, Label = new(ar, en), Min = min, Max = max, Group = group };

    public static FieldDef Days(string key, string en, string ar, string? group = null) =>
        new() { Key = key, Type = FieldType.Days, Label = new(ar, en), Group = group };

    public static FieldDef Time(string key, string en, string ar, string? group = null) =>
        new() { Key = key, Type = FieldType.Time, Label = new(ar, en), Group = group };

    public static FieldDef List(string key, string en, string ar, string itemLabel, IReadOnlyList<FieldDef> items, int max = 20, int min = 0, string? group = null) =>
        new() { Key = key, Type = FieldType.List, Label = new(ar, en), ItemFields = items, ItemLabel = itemLabel, MaxItems = max, MinItems = min, Group = group };

    public static OptionDef Opt(string value, string en, string ar) => new(value, new(ar, en));

    public static FieldDef WithHelp(this FieldDef f, string en, string ar) => f with { Help = new(ar, en) };
}
