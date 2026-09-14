using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class TransformParseBridge
{
    internal static Func<string, SeriesTransformBlock>? ParseTransformFile { get; set; }
}
