namespace DashSpec.Core.Model;

public sealed record PresentationBlock(
    string? UsePreset,
    IReadOnlyDictionary<string, string> Properties);

public sealed record SeriesTransformBlock(
    string? UsePreset,
    int? Max,
    string? OtherLabel);

public sealed record SeriesTransformSettings(int Max, string OtherLabel)
{
    public static SeriesTransformSettings? FromBlock(SeriesTransformBlock? block)
    {
        if (block?.Max is int maxValue && maxValue > 0)
        {
            return new SeriesTransformSettings(maxValue, block.OtherLabel ?? "Other");
        }

        return null;
    }
}
