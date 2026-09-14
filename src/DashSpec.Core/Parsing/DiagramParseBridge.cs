using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DiagramParseBridge
{
    internal static Func<string, string?, SpecIncludeFragment>? ParseDiagramFile { get; set; }

    internal static Func<string, string?, (string Id, SpecIncludeFragment Fragment)>? ParseDiagramFileWithId { get; set; }
}
