// Runtime interaction DTO for heatmap-cell click drill (ADR-0028 / ADR-0064).
// Not spec model: it moved out of the retired DashSpec.Core.Model namespace (ADR-0048).
namespace DashSpec.Core.Interaction;

using DashSpec.Execution.Runtime;

public sealed record HeatmapCellContext(
    int Yi,
    int Xi,
    string XLabel,
    string YLabel,
    double? Value,
    string? TooltipRaw,
    DateTime? XBucketUtc = null,
    DateTime? XBucketEndUtc = null,
    // ADR-0107: click source — the plugin-declared surface that was clicked
    // (e.g. matrix "category item"); null = the generic click (cells, chart categories).
    string? ClickSource = null,
    // Граф (G), которым были получены данные этой ячейки: клик-значения — в его
    // display-кадре, поэтому для фильтров по storage-колонкам применяется G⁻¹.
    DashflowExecutionPath? FlowPath = null);
