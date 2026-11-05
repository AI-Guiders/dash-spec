using DashSpec.Modeling.Parse.Layout;
using Microsoft.FSharp.Core;
using LayoutModuleParser = DashSpec.Execution.Parsing.LayoutModuleParser;

namespace DashSpec.Composition.Shell;

/// <summary>
/// Embedded default shell board (ADR-0111 §2): the <c>host.ds</c> grid drawing is the single
/// no-silent-code default for documents that draw no grid of their own. Kernel-owned so every
/// composition root (web Host, dsc CLI) registers the same default — vocabulary parity
/// (ADR-0117 decision 3) includes the layout default.
/// </summary>
public static class DashSpecDefaultShell
{
    private const string EmbeddedResourceName = "DashSpec.Composition.Resources.host.ds";

    private static LayoutBoardDefinition? _board;

    /// <summary>The embedded default shell board: a regular ds layout module — a <c>grid</c>
    /// drawing plus the shell board — not code.</summary>
    public static LayoutBoardDefinition Board()
    {
        if (_board is { } board)
        {
            return board;
        }

        using var stream = typeof(DashSpecDefaultShell).Assembly
            .GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded default shell board not found: '{EmbeddedResourceName}'. Available: " +
                string.Join(", ", typeof(DashSpecDefaultShell).Assembly.GetManifestResourceNames()));

        using var reader = new StreamReader(stream);
        return _board = LayoutModuleParser.ParseLayoutFile(reader.ReadToEnd());
    }

    /// <summary>The host default grid ([ADR-0111] §2) — from the embedded board, never code.</summary>
    public static LayoutDefinition DefaultGrid()
    {
        var board = Board();
        if (board.GridColumns <= 0)
        {
            throw new InvalidOperationException(
                "Embedded default host.ds must carry a grid drawing: a 'grid [] [] …' block is required.");
        }

        return new LayoutDefinition { Columns = board.GridColumns };
    }

    /// <summary>Registers the grid default for the current process (both roots call this).</summary>
    public static void RegisterGridDefault() =>
        GridDefaultBridge.Default = FSharpOption<LayoutDefinition>.Some(DefaultGrid());
}
