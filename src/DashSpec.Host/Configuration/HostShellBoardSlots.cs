using DashSpec.Abstractions.Viewer;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;
using ExecutionLayoutModuleParser = DashSpec.Execution.Parsing.LayoutModuleParser;

namespace DashSpec.Host.Configuration;

/// <summary>
/// Shell grid slots ([ADR-0111] §2): the scope-host layout board from the host module is the
/// shell grid itself. No synthesized default: when the host module carries no board, the
/// embedded <c>host.ds</c> default board serves — a regular ds file shipped as an
/// EmbeddedResource, not code.
/// </summary>
public static class HostShellBoardSlots
{
    private const string EmbeddedResourceName = "DashSpec.Host.Resources.host.ds";

    /// <summary>
    /// The embedded default shell board (<c>host.ds</c>): a regular ds layout module —
    /// brand + nav topbar row, content below — not code.
    /// </summary>
    public static LayoutBoardDefinition EmbeddedDefaultBoard()
    {
        var assembly = typeof(HostShellBoardSlots).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded default shell board not found: '{EmbeddedResourceName}'.");

        using var reader = new StreamReader(stream);
        return ExecutionLayoutModuleParser.ParseLayoutFile(reader.ReadToEnd());
    }

    /// <summary>Resolve the shell grid slots: the host module board, or the embedded default.</summary>
    public static IReadOnlyList<ViewerShellSlot> Resolve(LayoutBoardDefinition? board) =>
        ShellBoardSlotResolver.Resolve(board ?? EmbeddedDefaultBoard());
}
