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

        // Row 1: weights 3/1/2 of 6 on 12 columns → spans 6/2/remainder 4.
        Assert.Equal(new ViewerShellSlot("brand", 1, 1, 6), Find(slots, "brand"));
        Assert.Equal(new ViewerShellSlot("catalog", 1, 7, 2), Find(slots, "catalog"));
        Assert.Equal(new ViewerShellSlot("nav", 1, 9, 4), Find(slots, "nav"));

        // Row 2: single cell — the whole width.
        Assert.Equal(new ViewerShellSlot("content", 2, 1, 12), Find(slots, "content"));
    }

    [Fact]
    public void Equal_weight_row_shares_the_full_width()
    {
        // The LUS corpus row: six chrome slots, weight 1 each — the row fills
        // the whole width (2 columns each), never a squeezed left slice.
        var board = ParseBoard("[ brand catalog nav external_links help settings ]");

        var slots = ShellBoardSlotResolver.Resolve(board);

        var chromeRow = slots.Where(slot => slot.Row == 1).OrderBy(slot => slot.Col).ToList();
        Assert.Equal(6, chromeRow.Count);
        Assert.All(chromeRow, slot => Assert.Equal(2, slot.Span));
        Assert.Equal(1, chromeRow[0].Col);
        Assert.Equal(11, chromeRow[^1].Col);
        Assert.Equal(12, chromeRow[^1].Col + chromeRow[^1].Span - 1);
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

    static ViewerShellSlot Find(IReadOnlyList<ViewerShellSlot> slots, string slotId) =>
        Assert.Single(slots, slot => slot.SlotId == slotId);

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
        Assert.Equal(new ViewerShellSlot("brand", 1, 1, 6), Find(slots, "brand"));
        Assert.Equal(new ViewerShellSlot("nav", 1, 7, 6), Find(slots, "nav"));
        Assert.Equal(new ViewerShellSlot("content", 2, 1, 12), Find(slots, "content"));
    }
}
