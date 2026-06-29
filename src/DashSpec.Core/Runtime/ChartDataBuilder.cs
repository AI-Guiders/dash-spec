using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

/// <summary>Facade: row sets → chart/table/matrix payloads by diagram kind.</summary>
public static class ChartDataBuilder
{
    public static ChartPayload BuildLineOrBar(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram,
        SeriesTransformSettings? seriesTransform = null) =>
        ChartSeriesPayloadBuilder.Build(rows, diagram, seriesTransform);

    public static TablePayload BuildTable(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram) =>
        TablePayloadBuilder.Build(rows, diagram);

    public static MatrixPayload BuildHeatmap(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram) =>
        MatrixPayloadBuilder.Build(rows, diagram);
}

public sealed record ChartPayload(IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series);

public sealed record ChartSeries(string Name, IReadOnlyList<double?> Values);

public sealed record TablePayload(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record MatrixPayload(
    IReadOnlyList<string> XLabels,
    IReadOnlyList<string> YLabels,
    double?[][] Cells,
    double Min,
    double Max,
    string?[][]? Tooltips = null);
