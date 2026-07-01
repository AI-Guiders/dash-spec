using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

public static class CardChromeResolver
{
    public static ChartPresentation ResolveChartPresentation(
        CardDefinition card,
        SpecLibrary? library)
    {
        var props = MergePresentationProperties(card, library);
        return ChartPresentation.FromProperties(props);
    }

    public static int ResolveMatrixHeightPx(CardDefinition card, SpecLibrary? library)
    {
        var props = MergePresentationProperties(card, library);
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

    private static Dictionary<string, string> MergePresentationProperties(
        CardDefinition card,
        SpecLibrary? library)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (card.Presentation?.UsePreset is { } presetName &&
            library?.TryGetPresentation(presetName) is { } preset)
        {
            foreach (var (key, value) in preset)
            {
                merged[key] = value;
            }
        }

        if (card.Presentation is not null)
        {
            foreach (var (key, value) in card.Presentation.Properties)
            {
                if (!string.Equals(key, "use", StringComparison.OrdinalIgnoreCase))
                {
                    merged[key] = value;
                }
            }
        }

        foreach (var legacyKey in new[]
        {
            "legend", "height", "stacked", "orientation",
            "scale_value", "scale_measure", "scale_x", "scale_y", "value_scale", "y_format",
        })
        {
            if (!merged.ContainsKey(legacyKey) &&
                card.Diagram.Properties.TryGetValue(legacyKey, out var legacyValue))
            {
                merged[legacyKey] = legacyValue;
            }
        }

        return merged;
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
}
