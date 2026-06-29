namespace DashSpec.Core.Model;

public sealed record LegendDefinition(
    string? MinLabel = null,
    string? MaxLabel = null,
    string? Title = null);
