using System.Text;
using System.Text.RegularExpressions;

namespace Qoot.Api.Infrastructure;

/// <summary>
/// Normalises text typed into the dashboard or the public form. Content is stored and rendered as
/// plain text (the website inserts it with textContent), so no HTML is ever interpreted; this keeps
/// stray control characters and directional overrides out of the database.
/// </summary>
public static partial class SafeText
{
    public static string Clean(string? input, bool multiline = false)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "";
        }

        var s = input.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '\n':
                    sb.Append(multiline ? '\n' : ' ');
                    continue;
                case '\t':
                    sb.Append(' ');
                    continue;
                // Left-to-right / right-to-left overrides can disguise text; marks (U+200E/F) and isolates stay.
                case '‭' or '‮':
                    continue;
            }

            if (char.IsControl(ch))
            {
                continue;
            }

            sb.Append(ch);
        }

        var result = sb.ToString().Trim();
        if (multiline)
        {
            result = ManyNewlines().Replace(result, "\n\n");
            result = string.Join('\n', result.Split('\n').Select(line => line.TrimEnd()));
        }
        else
        {
            result = ManySpaces().Replace(result, " ");
        }

        return result;
    }

    /// <summary>
    /// Link targets allowed anywhere on the site: an anchor (#faq), a site path (/news/), an absolute
    /// http(s) address, mailto: or tel:. Everything else (javascript:, data:, //host ...) is refused.
    /// </summary>
    public static bool IsSafeLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500 || url.Any(char.IsWhiteSpace) && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (url.StartsWith('#'))
        {
            return Anchor().IsMatch(url);
        }

        if (url.StartsWith('/'))
        {
            return !url.StartsWith("//", StringComparison.Ordinal) && !url.Contains('\\', StringComparison.Ordinal) && SitePath().IsMatch(url);
        }

        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return MailTo().IsMatch(url);
        }

        if (url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return Tel().IsMatch(url);
        }

        return IsAbsoluteHttpUrl(url);
    }

    public static bool IsAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrEmpty(uri.Host)
        && !url.Any(char.IsWhiteSpace);

    public static bool IsEmail(string value) => value.Length <= 254 && Email().IsMatch(value);

    public static bool IsPhone(string value) => Phone().IsMatch(value);

    /// <summary>Accepts what people type as a phone number: +218 91 234 5678, (091) 234-5678 ...</summary>
    public static bool LooksLikePhone(string value)
    {
        var digits = value.Count(char.IsAsciiDigit);
        return digits is >= 6 and <= 15 && LoosePhone().IsMatch(value);
    }

    public static bool IsSlug(string value) => value.Length is >= 3 and <= 120 && Slug().IsMatch(value);

    public static string Slugify(string text)
    {
        var sb = new StringBuilder();
        var dash = false;
        foreach (var ch in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(ch);
                dash = false;
            }
            else if (!dash && sb.Length > 0 && (char.IsWhiteSpace(ch) || ch is '-' or '_' or '.' or '/' or '&'))
            {
                sb.Append('-');
                dash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return slug.Length > 80 ? slug[..80].Trim('-') : slug;
    }

    [GeneratedRegex(@"^#[A-Za-z][A-Za-z0-9_-]{0,60}$")]
    private static partial Regex Anchor();

    [GeneratedRegex(@"^/[A-Za-z0-9._~!$&'()*+,;=:@%/?#-]*$")]
    private static partial Regex SitePath();

    [GeneratedRegex(@"^mailto:[^\s@<>""']+@[^\s@<>""']+(\?[^\s<>""']*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex MailTo();

    [GeneratedRegex(@"^tel:\+?[0-9]{3,16}$", RegexOptions.IgnoreCase)]
    private static partial Regex Tel();

    [GeneratedRegex(@"^[^\s@<>()"",;:\\]+@[^\s@<>()"",;:\\]+\.[^\s@<>()"",;:\\]{2,}$")]
    private static partial Regex Email();

    [GeneratedRegex(@"^\+?[0-9]{6,15}$")]
    private static partial Regex Phone();

    [GeneratedRegex(@"^\+?[0-9 ()\-.]{6,24}$")]
    private static partial Regex LoosePhone();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Slug();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlines();

    [GeneratedRegex(@" {2,}")]
    private static partial Regex ManySpaces();
}
