using DashSpec.Plugin.Viz.Builtins.Resources;
using Microsoft.Extensions.Localization;

namespace DashSpec.Plugin.Viz.Builtins.Components.Charts;

/// <summary>Composes the matrix oversize detail line from this plugin's own copy (ADR-0077).</summary>
internal static class MatrixOversizeMessage
{
    public static string Format(
        IStringLocalizer<VizStrings> loc,
        MatrixRenderLimitsDefinition limits,
        int xCount,
        int yCount)
    {
        if (!limits.IsOversized(xCount, yCount))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var cells = xCount * yCount;
        if (cells > limits.EffectiveMaxCells)
        {
            parts.Add(loc["Matrix oversize: cells", xCount, yCount, cells, limits.EffectiveMaxCells]);
        }

        if (xCount > limits.EffectiveMaxAxisLabels)
        {
            parts.Add(loc["Matrix oversize: X columns", xCount, limits.EffectiveMaxAxisLabels]);
        }

        if (yCount > limits.EffectiveMaxAxisLabels)
        {
            parts.Add(loc["Matrix oversize: Y rows", yCount, limits.EffectiveMaxAxisLabels]);
        }

        return string.Join("; ", parts);
    }
}
