using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class FiltersChromeStickyParser
{
    public static string Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return FiltersChromeDefinition.StickyNone;
        }

        var value = raw.Trim().ToLowerInvariant();
        return value switch
        {
            "true" or "yes" or "1" => FiltersChromeDefinition.StickyLine,
            "false" or "no" or "0" => FiltersChromeDefinition.StickyNone,
            FiltersChromeDefinition.StickyNone => FiltersChromeDefinition.StickyNone,
            FiltersChromeDefinition.StickyLine => FiltersChromeDefinition.StickyLine,
            FiltersChromeDefinition.StickyCard => FiltersChromeDefinition.StickyCard,
            _ => throw new DashSpecParseException(
                "filters chrome sticky must be 'none', 'line', or 'card' (true/false also accepted)."),
        };
    }
}
