using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class CardLimitsParser
{
    public static MatrixRenderLimitsDefinition Parse(TokenReader reader, string cardId)
    {
        var props = PropertyBlockParser.Parse(
            reader,
            PropertySchemas.CardLimits,
            $"card '{cardId}' limits",
            allowExtensionProperties: false);

        int? maxCells = null;
        int? maxAxisLabels = null;

        if (props.TryGetValue("cells", out var cellsRaw))
        {
            if (!int.TryParse(cellsRaw, out var cells) || cells <= 0)
            {
                throw new DashSpecParseException($"Card '{cardId}': limits.cells must be a positive integer.");
            }

            maxCells = cells;
        }

        if (props.TryGetValue("axis", out var axisRaw))
        {
            if (!int.TryParse(axisRaw, out var axis) || axis <= 0)
            {
                throw new DashSpecParseException($"Card '{cardId}': limits.axis must be a positive integer.");
            }

            maxAxisLabels = axis;
        }

        if (maxCells is null && maxAxisLabels is null)
        {
            throw new DashSpecParseException($"Card '{cardId}': limits block must set cells and/or axis.");
        }

        return new MatrixRenderLimitsDefinition(maxCells, maxAxisLabels);
    }
}
