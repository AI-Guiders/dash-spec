
namespace DashSpec.Core.Parsing;

internal static class TransformParseBridge
{
    internal static Func<string, SeriesTransformBlock>? ParseTransformFile { get; set; }
}
