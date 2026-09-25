using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Runtime;

/// <summary>Diagram / chart composition merge entry point (ADR-0057 §6, ADR-0039).</summary>
public static class CompositionResolution
{
    public static IReadOnlyDictionary<string, string> ResolveChartChromeProperties(
        CardDefinition card,
        SpecLibrary? library) =>
        ChartChromeProperties.Merge(card, library);

    public static ChartPresentation ResolveChartPresentation(
        CardDefinition card,
        SpecLibrary? library) =>
        CardChromeResolver.ResolveChartPresentation(card, library);

    public static SeriesTransformSettings? ResolveSeriesTransform(
        CardDefinition card,
        SpecLibrary? library) =>
        CardChromeResolver.ResolveSeriesTransform(card, library);

    public static int ResolveMatrixHeightPx(CardDefinition card, SpecLibrary? library) =>
        CardChromeResolver.ResolveMatrixHeightPx(card, library);

    public static int? ResolveVisibleRows(CardDefinition card, SpecLibrary? library) =>
        CardChromeResolver.ResolveVisibleRows(card, library);
}
