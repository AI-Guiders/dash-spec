using DashSpec.Abstractions.Viewer;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;
using Microsoft.FSharp.Core;
using Xunit;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using ExecutionLayoutModuleParser = DashSpec.Execution.Parsing.LayoutModuleParser;

namespace DashSpec.Core.Tests;

/// <summary>
/// Shell board slots ([ADR-0111] §2): the scope-host layout board from the host module is the
/// shell grid — chrome slots plus the content slot, placed as recorded in ds; unknown slots
/// fail loud; the implicit content slot is a recorded default, not a synthesized grid.
/// </summary>
public sealed class ShellBoardSlotResolverTests
{
    public ShellBoardSlotResolverTests() => DashSpecParser.EnsureModuleParsersRegistered();

    /// <summary>Parse a scope-host board from ds text — the same grammar the host module imports.</summary>
    static LayoutBoardDefinition ParseBoard(params string[] rows)
    {
        var body = string.Join("\n", rows.Select(static row => $"    {row}"));
        return ExecutionLayoutModuleParser.ParseLayoutFile($"""
            module layout test_shell
            namespace Test.Shell
            scope host

            {body}
            """);
    }

    [Fact]
    public void Declared_board_places_slots_as_recorded()
    {
        var board = ParseBoard(
            "[ brand:3 catalog nav:2 ]",
            "[ content ]");

        var slots = ShellBoardSlotResolver.Resolve(board);

        Assert.Equal(4, slots.Count);
        Assert.Equal(new ViewerShellSlot("brand", 1, 1, 3), slots[0]);
        Assert.Equal(new ViewerShellSlot("catalog", 1, 2, 1), slots[1]);
        Assert.Equal(new ViewerShellSlot("nav", 1, 3, 2), slots[2]);
        Assert.Equal(new ViewerShellSlot("content", 2, 1, 1), slots[3]);
    }

    [Fact]
    public void Content_slot_is_implicit_full_width_tail_when_not_declared()
    {
        var board = ParseBoard("[ brand nav ]");

        var slots = ShellBoardSlotResolver.Resolve(board);

        var content = Assert.Single(slots, slot => slot.SlotId == ViewerShellSlotIds.Content);
        Assert.Equal(2, content.Row);
        Assert.Equal(1, content.Col);
        Assert.Equal(ShellBoardSlotResolver.GridColumns, content.Span);
    }

    [Fact]
    public void Unknown_slot_fails_loud_with_token_and_row()
    {
        var board = ParseBoard("[ brand mystery ]");

        var ex = Assert.Throws<DashSpecParseException>(() => ShellBoardSlotResolver.Resolve(board));
        Assert.Contains("mystery", ex.Message);
        Assert.Contains("row 1", ex.Message);
    }

    [Fact]
    public void Legacy_aliases_normalize_to_current_slot_ids()
    {
        var board = ParseBoard("[ report_picker dashboard ]");

        var slots = ShellBoardSlotResolver.Resolve(board);

        Assert.Contains(slots, slot => slot.SlotId == ViewerShellSlotIds.Catalog);
        Assert.Contains(slots, slot => slot.SlotId == ViewerShellSlotIds.Nav);
    }

    [Fact]
    public void Missing_or_empty_board_fails_loud()
    {
        Assert.Throws<DashSpecParseException>(() => ShellBoardSlotResolver.Resolve(null));
        Assert.Throws<DashSpecParseException>(() => ShellBoardSlotResolver.Resolve(
            ParseBoard("[ ]")));
    }

    [Fact]
    public void Embedded_default_board_text_parses_as_regular_ds()
    {
        // The shipped default (host.ds as EmbeddedResource in DashSpec.Host) is a regular
        // ds layout module — this pins its shape: brand + nav topbar row, content below.
        var board = ExecutionLayoutModuleParser.ParseLayoutFile("""
            module layout dashspec_default_shell
            namespace DashSpec.Default.Shell
            scope host

            [ brand nav ]
            [ content ]
            """);

        Assert.Equal(LayoutScope.Host, board.ModuleScope.Value);

        var slots = ShellBoardSlotResolver.Resolve(board);
        Assert.Equal(3, slots.Count);
        Assert.Equal(new ViewerShellSlot("brand", 1, 1, 1), slots[0]);
        Assert.Equal(new ViewerShellSlot("nav", 1, 2, 1), slots[1]);
        Assert.Equal(new ViewerShellSlot("content", 2, 1, 1), slots[2]);
    }
}
