// CardClickBehaviour and click effects are owned by DashSpec.Modeling.Parse.Card (F#).
namespace DashSpec.Core.Model;

public sealed record HeatmapCellContext(
    int Yi,
    int Xi,
    string XLabel,
    string YLabel,
    double? Value,
    string? TooltipRaw,
    DateTime? XBucketUtc = null);
