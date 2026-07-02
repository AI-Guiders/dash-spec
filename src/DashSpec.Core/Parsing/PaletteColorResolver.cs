namespace DashSpec.Core.Parsing;

/// <summary>Resolves palette color operands (hex strings, CSS names, <c>const</c> refs) to <c>#rrggbb</c>.</summary>
internal static class PaletteColorResolver
{
    private static readonly Dictionary<string, string> CssColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = "#f0f8ff",
        ["antiquewhite"] = "#faebd7",
        ["aqua"] = "#00ffff",
        ["aquamarine"] = "#7fffd4",
        ["azure"] = "#f0ffff",
        ["beige"] = "#f5f5dc",
        ["bisque"] = "#ffe4c4",
        ["black"] = "#000000",
        ["blue"] = "#0000ff",
        ["brown"] = "#a52a2a",
        ["cyan"] = "#00ffff",
        ["gold"] = "#ffd700",
        ["gray"] = "#808080",
        ["green"] = "#008000",
        ["grey"] = "#808080",
        ["indigo"] = "#4b0082",
        ["lime"] = "#00ff00",
        ["magenta"] = "#ff00ff",
        ["maroon"] = "#800000",
        ["navy"] = "#000080",
        ["olive"] = "#808000",
        ["orange"] = "#ffa500",
        ["pink"] = "#ffc0cb",
        ["purple"] = "#800080",
        ["red"] = "#ff0000",
        ["silver"] = "#c0c0c0",
        ["slate"] = "#708090",
        ["teal"] = "#008080",
        ["violet"] = "#ee82ee",
        ["white"] = "#ffffff",
        ["yellow"] = "#ffff00",
    };

    public static string ResolveOperand(
        string raw,
        IReadOnlyDictionary<string, string> constants,
        string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        if (TryResolveHexLiteral(raw, out var hex))
        {
            return hex;
        }

        if (constants.TryGetValue(raw, out var constantHex))
        {
            return constantHex;
        }

        if (CssColors.TryGetValue(raw, out var cssHex))
        {
            return cssHex;
        }

        throw new DashSpecParseException(
            $"Unknown color '{raw}' in {context}. Use \"#rrggbb\", a CSS color name, or a const reference.");
    }

    public static string JoinColorList(IEnumerable<string> resolvedHex) =>
        string.Join(',', resolvedHex);

    private static bool TryResolveHexLiteral(string raw, out string hex)
    {
        hex = string.Empty;
        var value = raw.Trim();
        if (!value.StartsWith('#'))
        {
            return false;
        }

        var digits = value[1..];
        if (digits.Length is not (3 or 6) ||
            !digits.All(static c => Uri.IsHexDigit(c)))
        {
            throw new DashSpecParseException($"Invalid hex color '{raw}'.");
        }

        hex = digits.Length is 3
            ? $"#{digits[0]}{digits[0]}{digits[1]}{digits[1]}{digits[2]}{digits[2]}"
            : $"#{digits}".ToLowerInvariant();
        return true;
    }
}
