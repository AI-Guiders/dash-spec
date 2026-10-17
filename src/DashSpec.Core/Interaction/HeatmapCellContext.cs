// Runtime interaction DTO for heatmap-cell click drill (ADR-0028 / ADR-0064).
// Not spec model: it moved out of the retired DashSpec.Core.Model namespace (ADR-0048).
namespace DashSpec.Core.Interaction;

public sealed record HeatmapCellContext(
    int Yi,
    int Xi,
    string XLabel,
    string YLabel,
    double? Value,
    string? TooltipRaw,
    DateTime? XBucketUtc = null);
