using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DashSpec.Core.Runtime;

/// <summary>PlantUML-inspired inline markup for whitelisted spec strings. Not full Creole.</summary>
public static partial class CreoleSubset
{
    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#0f172a",
        ["white"] = "#f8fafc",
        ["blue"] = "#3b82f6",
        ["red"] = "#ef4444",
        ["green"] = "#22c55e",
        ["orange"] = "#f97316",
        ["gray"] = "#94a3b8",
        ["grey"] = "#94a3b8",
        ["silver"] = "#cbd5e1",
        ["yellow"] = "#eab308",
    };

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return RenderPlainAndStyled(text).ToString();
    }

    private static StringBuilder RenderPlainAndStyled(string text)
    {
        var styled = StyledMatches(text);
        var output = new StringBuilder();
        var last = 0;

        foreach (var (match, cssProperty) in styled)
        {
            output.Append(EncodeAndInline(text[last..match.Index]));
            var cssValue = ResolveColor(match.Groups[1].Value);
            if (cssValue is null)
            {
                output.Append(EncodeAndInline(match.Value));
            }
            else
            {
                output.Append($"""<span style="{cssProperty}:{cssValue}">""");
                output.Append(RenderPlainAndStyled(match.Groups[2].Value));
                output.Append("</span>");
            }

            last = match.Index + match.Length;
        }

        output.Append(EncodeAndInline(text[last..]));
        return output;
    }

    private static List<(Match Match, string CssProperty)> StyledMatches(string text)
    {
        var matches = new List<(Match Match, string CssProperty)>();
        foreach (Match match in ColorTag().Matches(text))
        {
            matches.Add((match, "color"));
        }

        foreach (Match match in BackTag().Matches(text))
        {
            matches.Add((match, "background"));
        }

        return matches.OrderBy(m => m.Match.Index).ToList();
    }

    private static string EncodeAndInline(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return string.Empty;
        }

        var encoded = WebUtility.HtmlEncode(segment);
        encoded = Inline("**").Replace(encoded, "<strong>$1</strong>");
        encoded = Inline("//").Replace(encoded, "<em>$1</em>");
        encoded = Mono().Replace(encoded, "<code>$1</code>");
        encoded = Inline("__").Replace(encoded, "<u>$1</u>");
        encoded = Inline("--").Replace(encoded, "<s>$1</s>");
        return encoded;
    }

    private static Regex Inline(string delimiter) =>
        new(Regex.Escape(delimiter) + "(.+?)" + Regex.Escape(delimiter), RegexOptions.None, TimeSpan.FromMilliseconds(50));

    private static string? ResolveColor(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.StartsWith("#", StringComparison.Ordinal))
        {
            return HexColor().IsMatch(raw) ? raw : null;
        }

        return NamedColors.GetValueOrDefault(raw.Trim());
    }

    [GeneratedRegex(@"<color:([^>]+)>(.+?)</color>", RegexOptions.IgnoreCase)]
    private static partial Regex ColorTag();

    [GeneratedRegex(@"<back:([^>]+)>(.+?)</back>", RegexOptions.IgnoreCase)]
    private static partial Regex BackTag();

    [GeneratedRegex(@"""([^""]+?)""")]
    private static partial Regex Mono();

    [GeneratedRegex(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();
}
