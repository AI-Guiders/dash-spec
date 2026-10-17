
namespace DashSpec.Core.Parsing;

internal static class DiagramParseBridge
{
    internal static Func<string, string?, UnitFragment>? ParseDiagramFile { get; set; }

    internal static Func<string, string?, (string Id, UnitFragment Fragment)>? ParseDiagramFileWithId { get; set; }
}
