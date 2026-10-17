
namespace DashSpec.Core.Parsing;

/// <summary>Runtime hook for F# layout SSOT without Core→Modeling.Parse cycle.</summary>
internal static class LayoutParseBridge
{
    internal static Func<string, LayoutBoardDefinition>? ParseLayoutFile { get; set; }
}