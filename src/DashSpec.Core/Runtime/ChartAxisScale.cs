using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

/// <summary>
/// Linear axis scale for line/bar measure.
/// Bar: prefer <c>scale_value</c> (aliases: <c>scale_measure</c>, <c>scale_y</c>).
/// </summary>
public enum ChartAxisScale
{
    Decimal,
    Integer,
    Percent,
}

public static class ChartAxisScaleParser
{
    public static ChartAxisScale ParseScale(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "integer" or "int" or "count" => ChartAxisScale.Integer,
            "percent" or "pct" or "percentage" or "%" => ChartAxisScale.Percent,
            "decimal" or "number" or "float" or "auto" => ChartAxisScale.Decimal,
            null or "" => ChartAxisScale.Decimal,
            _ => throw new DashSpecParseException(
                $"Unknown axis scale '{raw}'. Use integer, decimal, or percent."),
        };

    /// <summary>Scale for the numeric measure binding (<c>value</c> / <c>y</c>).</summary>
    public static ChartAxisScale ResolveValueAxis(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("scale_value", out var scaleValue))
        {
            return ParseScale(scaleValue);
        }

        if (properties.TryGetValue("scale_measure", out var scaleMeasure))
        {
            return ParseScale(scaleMeasure);
        }

        if (properties.TryGetValue("scale_y", out var scaleY))
        {
            return ParseScale(scaleY);
        }

        if (properties.TryGetValue("value_scale", out var legacyValueScale))
        {
            return ParseScale(legacyValueScale);
        }

        if (properties.TryGetValue("y_format", out var legacyYFormat) &&
            IsIntegerAlias(legacyYFormat))
        {
            return ChartAxisScale.Integer;
        }

        if (properties.TryGetValue("scale_x", out var scaleX))
        {
            return ParseScale(scaleX);
        }

        return ChartAxisScale.Decimal;
    }

    private static bool IsIntegerAlias(string raw) =>
        raw.Trim().ToLowerInvariant() is "integer" or "int" or "count";
}
