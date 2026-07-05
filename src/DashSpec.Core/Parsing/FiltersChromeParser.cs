using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class FiltersChromeParser
{
    public static FiltersChromeDefinition Parse(TokenReader reader)
    {
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.FiltersChrome, "filters chrome");

        var layout = "card";
        var sticky = FiltersChromeDefinition.StickyNone;
        var apply = "manual";
        var debounceMs = 400;

        if (props.TryGetValue("layout", out var layoutRaw))
        {
            layout = layoutRaw.ToLowerInvariant() switch
            {
                "card" or "bar" => layoutRaw.ToLowerInvariant(),
                _ => throw new DashSpecParseException("filters chrome layout must be 'card' or 'bar'."),
            };
        }

        if (props.TryGetValue("sticky", out var stickyRaw))
        {
            sticky = FiltersChromeStickyParser.Parse(stickyRaw);
        }

        if (props.TryGetValue("apply", out var applyRaw))
        {
            apply = applyRaw.ToLowerInvariant() switch
            {
                "manual" or "auto" => applyRaw.ToLowerInvariant(),
                _ => throw new DashSpecParseException("filters chrome apply must be 'manual' or 'auto'."),
            };
        }

        if (props.TryGetValue("debounce_ms", out var debounceRaw) &&
            int.TryParse(debounceRaw, out var parsedDebounce) &&
            parsedDebounce >= 0)
        {
            debounceMs = parsedDebounce;
        }

        return new FiltersChromeDefinition(layout, sticky, apply, debounceMs);
    }
}
