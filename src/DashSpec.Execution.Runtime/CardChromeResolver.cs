using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Runtime;

public static class CardChromeResolver
{
    public static ChartPresentation ResolveChartPresentation(
        CardDefinition card,
        SpecLibrary? library)
    {
        var props = ChartChromeProperties.Merge(card, library);
        var presentation = ChartPresentation.FromProperties(props, diagramKind: card.Diagram.Kind);
        var valueAxisLabel = DiagramBindings.TryGetColumn(card.Diagram, "value", out _)
            ? DiagramBindings.Label(card.Diagram, "value") ?? DiagramBindings.Label(card.Diagram, "y")
            : DiagramBindings.Label(card.Diagram, "y");

        return presentation with
        {
            CategoryAxisLabel = DiagramBindings.Label(card.Diagram, "x"),
            ValueAxisLabel = valueAxisLabel,
        };
    }

    public static int ResolveMatrixHeightPx(CardDefinition card, SpecLibrary? library)
    {
        var props = ChartChromeProperties.Merge(card, library);
        if (props.TryGetValue("height", out var rawHeight) &&
            int.TryParse(rawHeight, out var parsedHeight) &&
            parsedHeight is >= 160 and <= 800)
        {
            return parsedHeight;
        }

        if (card.Diagram.Properties.TryGetValue("height", out var legacyHeight) &&
            int.TryParse(legacyHeight, out var legacyParsed) &&
            legacyParsed is >= 160 and <= 800)
        {
            return legacyParsed;
        }

        return 320;
    }

    public static int? ResolveMatrixVisibleRows(CardDefinition card, SpecLibrary? library)
    {
        var props = ChartChromeProperties.Merge(card, library);
        if (TryParsePositiveInt(props, "visible_rows", out var fromChrome))
        {
            return fromChrome;
        }

        if (TryParsePositiveInt(card.Diagram.Properties, "visible_rows", out var fromDiagram))
        {
            return fromDiagram;
        }

        var series = ResolveSeriesTransform(card, library);
        return series is { Max: > 0 } ? series.Max : null;
    }

    public static SeriesTransformSettings? ResolveSeriesTransform(
        CardDefinition card,
        SpecLibrary? library)
    {
        var fromBlock = ResolveSeriesTransformFromBlock(card.SeriesTransform, library);
        if (fromBlock is not null)
        {
            return fromBlock;
        }

        if (card.Diagram.Properties.TryGetValue("max_series", out var raw) &&
            int.TryParse(raw, out var max) &&
            max > 0)
        {
            return new SeriesTransformSettings(max, "Other");
        }

        return null;
    }

    private static SeriesTransformSettings? ResolveSeriesTransformFromBlock(
        SeriesTransformBlock? block,
        SpecLibrary? library)
    {
        if (block is null)
        {
            return null;
        }

        if (block.UsePreset is { } presetName &&
            library?.TryGetSeriesTransform(presetName) is { } preset)
        {
            return new SeriesTransformSettings(
                preset.Max,
                block.OtherLabel ?? preset.OtherLabel);
        }

        if (block.Max is int maxValue && maxValue > 0)
        {
            return new SeriesTransformSettings(maxValue, block.OtherLabel ?? "Other");
        }

        return null;
    }

    private static bool TryParsePositiveInt(
        IReadOnlyDictionary<string, string> properties,
        string key,
        out int value)
    {
        value = 0;
        return properties.TryGetValue(key, out var raw) &&
               int.TryParse(raw, out value) &&
               value > 0;
    }
}
