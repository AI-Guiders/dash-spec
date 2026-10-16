namespace DashSpec.Core.Model;

public sealed record PresentationBlock(
    string? UsePreset,
    IReadOnlyDictionary<string, string> Properties);

public sealed record SeriesTransformBlock(
    string? UsePreset,
    int? Max,
    string? OtherLabel);
