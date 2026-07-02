using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Resolution;

public sealed record ResolvedSpecExport(
    string Id,
    string Title,
    string? ColorPalette,
    string? DiagramLibraryPath,
    IReadOnlyList<ResolvedCardExport> Cards);

public sealed record ResolvedCardExport(
    string Id,
    string Title,
    string? TabId,
    string DiagramKind,
    string? RenderPluginId,
    IReadOnlyDictionary<string, string> Diagram,
    string DataSourceKind,
    string DataSource,
    IReadOnlyList<string> BoundFilters,
    IReadOnlyList<string> LocalFilters,
    IReadOnlyDictionary<string, string>? ChartPresentation,
    IReadOnlyDictionary<string, string>? MatrixPresentation,
    IReadOnlyDictionary<string, string>? SeriesTransform,
    string? EffectiveColorPalette);

public static class SpecResolveExporter
{
    public static ResolvedSpecExport Export(DashboardDocument document, SpecLibrary? library)
    {
        var resolved = SpecResolver.Resolve(document, library);
        var cards = resolved.Cards
            .Select(view => ExportCard(document, view, library))
            .ToList();

        return new ResolvedSpecExport(
            document.Id,
            document.Title,
            document.ColorPalette,
            document.DiagramLibraryPath,
            cards);
    }

    private static ResolvedCardExport ExportCard(
        DashboardDocument document,
        ResolvedCardView view,
        SpecLibrary? library)
    {
        var card = view.Card;
        var kind = DiagramKindRegistry.Resolve(card.Diagram.Kind);
        var effectivePalette = ResolveEffectiveColorPalette(document.ColorPalette, card, library);

        IReadOnlyDictionary<string, string>? chartPresentation = null;
        IReadOnlyDictionary<string, string>? matrixPresentation = null;
        if (kind.DataFamily is DiagramDataFamily.Chart)
        {
            var presentation = ChartPresentation.FromProperties(
                MergeChartPresentationProperties(card, library));
            chartPresentation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["legend"] = presentation.Legend,
                ["height_px"] = presentation.HeightPx.ToString(),
                ["stacked"] = presentation.Stacked.ToString().ToLowerInvariant(),
                ["orientation"] = presentation.Orientation.ToString().ToLowerInvariant(),
            };
        }
        else if (kind.DataFamily is DiagramDataFamily.Matrix)
        {
            var matrix = MatrixPresentation.FromCard(card, library);
            matrixPresentation = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["height_px"] = matrix.HeightPx.ToString(),
                ["x_format"] = matrix.XFormat,
                ["y_format"] = matrix.YFormat,
                ["color_scale"] = matrix.ColorScale,
                ["tooltip_format"] = matrix.TooltipFormat.ToString().ToLowerInvariant(),
                ["tooltip_split"] = matrix.TooltipSplit,
            };
        }

        IReadOnlyDictionary<string, string>? seriesTransform = null;
        var transform = CardChromeResolver.ResolveSeriesTransform(card, library);
        if (transform is not null)
        {
            seriesTransform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["max"] = transform.Max.ToString(),
                ["other"] = transform.OtherLabel,
            };
        }

        return new ResolvedCardExport(
            card.Id,
            card.Title,
            card.TabId,
            card.Diagram.Kind,
            view.RenderPluginId,
            card.Diagram.Properties,
            card.DataSource.Kind.ToString().ToLowerInvariant(),
            FormatDataSource(card.DataSource),
            card.BoundFilters,
            card.LocalFilters,
            chartPresentation,
            matrixPresentation,
            seriesTransform,
            effectivePalette);
    }

    private static string FormatDataSource(DataSourceDefinition source) =>
        source.Kind switch
        {
            DataSourceKind.View => source.Value,
            DataSourceKind.Sql when source.SqlCarrier is DataSourceSqlCarrier.File => $"file:{source.Value}",
            DataSourceKind.Sql => source.Value,
            _ => source.Value,
        };

    private static string? ResolveEffectiveColorPalette(
        string? dashboardPalette,
        CardDefinition card,
        SpecLibrary? library)
    {
        if (card.Presentation?.Properties.TryGetValue("color_palette", out var inlinePalette) == true &&
            !string.IsNullOrWhiteSpace(inlinePalette))
        {
            return inlinePalette;
        }

        if (card.Presentation?.UsePreset is { } presetName &&
            library?.TryGetPresentation(presetName) is { } preset &&
            preset.TryGetValue("color_palette", out var presetPalette) &&
            !string.IsNullOrWhiteSpace(presetPalette))
        {
            return presetPalette;
        }

        if (card.Diagram.Properties.TryGetValue("color_palette", out var diagramPalette) &&
            !string.IsNullOrWhiteSpace(diagramPalette))
        {
            return diagramPalette;
        }

        return dashboardPalette;
    }

    private static Dictionary<string, string> MergeChartPresentationProperties(
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

        foreach (var legacyKey in new[] { "legend", "height", "stacked", "orientation", "color_palette" })
        {
            if (!merged.ContainsKey(legacyKey) &&
                card.Diagram.Properties.TryGetValue(legacyKey, out var legacyValue))
            {
                merged[legacyKey] = legacyValue;
            }
        }

        return merged;
    }
}
