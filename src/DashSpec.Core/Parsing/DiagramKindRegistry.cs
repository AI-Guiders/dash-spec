using DashSpec.Core.Model;
using FsharpDiagramKindRegistry = DashSpec.Modeling.Parse.Diagram.DiagramKindRegistry;

namespace DashSpec.Core.Parsing;

/// <summary>
/// Execution-facing facade over F# <see cref="FsharpDiagramKindRegistry"/> (ADR-0048 M6).
/// </summary>
public static class DiagramKindRegistry
{
    public static bool TryResolve(string kind, out DiagramKindInfo info)
    {
        if (FsharpDiagramKindRegistry.tryResolve(kind) is (true, { } spec))
        {
            info = new DiagramKindInfo(
                spec.Id,
                (DiagramDataFamily)(int)spec.DataFamily,
                spec.SupportsTopLimit,
                spec.AllowExtensionProperties);
            return true;
        }

        info = default!;
        return false;
    }

    public static DiagramKindInfo Resolve(string kind)
    {
        if (TryResolve(kind, out var info))
        {
            return info;
        }

        var known = string.Join(", ", FsharpDiagramKindRegistry.knownKinds());
        throw new ArgumentException($"Unknown diagram kind '{kind}'. Known kinds: {known}.");
    }

    public static bool SupportsTopLimit(string kind) =>
        FsharpDiagramKindRegistry.supportsTopLimit(kind);
}
