// Interaction DTO for heatmap-cell click drill (ADR-0028 / ADR-0064).
// Stays in the DashSpec.Core.Model namespace for consumer compatibility
// after the spec model moved to F# (ADR-0048).
namespace DashSpec.Core.Model;

public sealed record HeatmapCellContext(
    int Yi,
    int Xi,
    string XLabel,
    string YLabel,
    double? Value,
    string? TooltipRaw,
    DateTime? XBucketUtc = null);
