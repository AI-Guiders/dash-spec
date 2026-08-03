namespace DashSpec.Core.Model;

/// <summary>Derive a date filter range from another period anchor (ADR-0036).</summary>
public sealed record FilterDeriveDefinition(
    string TargetFilter,
    string SourceFilter,
    string? GrainFilterName = null);
