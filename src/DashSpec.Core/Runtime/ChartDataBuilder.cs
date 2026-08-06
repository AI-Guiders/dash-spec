using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

/// <summary>Facade: row sets → chart/table/matrix payloads by diagram kind.</summary>
public static class ChartDataBuilder
{
    public static ChartPayload BuildLineOrBar(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform,
        CardDefinition card,
        SpecLibrary? library,
        string? dashboardColorPalette = null) =>
        UsesCategoryAxis(diagram, rows)
            ? CategoryChartPayloadBuilder.Build(rows, diagram, seriesTransform, card, library, dashboardColorPalette)
            : ChartSeriesPayloadBuilder.Build(rows, diagram, seriesTransform, card, library, dashboardColorPalette);

    private static bool UsesCategoryAxis(DiagramDefinition diagram, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (diagram.Properties.ContainsKey("x_step"))
        {
            return false;
        }

        if (diagram.Properties.TryGetValue("series", out var seriesColumn) &&
            !string.IsNullOrWhiteSpace(seriesColumn))
        {
            return false;
        }

        if (!DiagramBindings.IsCategoryChart(diagram.Kind))
        {
            return false;
        }

        if (rows.Count == 0)
        {
            return true;
        }

        var xColumn = DiagramBindings.Column(diagram, "x");
        return TimeSeriesGrid.TryParseBucket(rows[0].GetValueOrDefault(xColumn)) is null;
    }

    public static TablePayload BuildTable(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram) =>
        TablePayloadBuilder.Build(rows, diagram);

    public static MatrixPayload BuildHeatmap(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform = null) =>
        MatrixPayloadBuilder.Build(rows, diagram, seriesTransform);
}

public sealed record ChartPayload(
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeries> Series,
    IReadOnlyList<double?>? ReferenceValues = null,
    string? ReferenceLabel = null);

public sealed record ChartSeries(
    string Name,
    IReadOnlyList<double?> Values,
    string? Color = null,
    IReadOnlyList<string>? PointColors = null);

public sealed record TablePayload(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record MatrixPayload(
    IReadOnlyList<string> XLabels,
    IReadOnlyList<string> YLabels,
    double?[][] Cells,
    double Min,
    double Max,
    string?[][]? Tooltips = null);
