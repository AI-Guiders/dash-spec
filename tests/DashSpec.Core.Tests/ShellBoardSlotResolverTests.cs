using DashSpec.Abstractions.Viewer;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;
using Microsoft.FSharp.Core;
using Xunit;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using ExecutionLayoutModuleParser = DashSpec.Execution.Parsing.LayoutModuleParser;

namespace DashSpec.Core.Tests;

/// <summary>
/// Shell board slots ([ADR-0111] §2 experiment): shell rows read as cells — chrome slots fit
/// their content (Span 0), a `.` dot is a rubber spacer sharing the row's free space by
/// weight, the content slot is greedy (Span 1) and takes the cell entirely; unknown slots
/// fail loud; the implicit content slot is a recorded default.
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
    public void Chrome_slots_fit_content_and_spacers_share_free_space_by_weight()
    {
        var board = ParseBoard("[ brand [] catalog nav []:2 help settings ]");

        var slots = ShellBoardSlotResolver.Resolve(board);

        var row1 = slots.Where(slot => slot.Row == 1).ToList();
        Assert.Equal(7, row1.Count);
        // Chrome slots: content-fit (Span 0), placed in cell order.
        Assert.Equal(new ViewerShellSlot("brand", 1, 1, 0), Find(row1, "brand"));
        Assert.Equal(new ViewerShellSlot("catalog", 1, 3, 0), Find(row1, "catalog"));
        Assert.Equal(new ViewerShellSlot("nav", 1, 4, 0), Find(row1, "nav"));
        Assert.Equal(new ViewerShellSlot("help", 1, 6, 0), Find(row1, "help"));
        Assert.Equal(new ViewerShellSlot("settings", 1, 7, 0), Find(row1, "settings"));
        // Spacers: rubber cells with weight (default 1, .:2 → 2).
        Assert.Equal(new ViewerShellSlot("[]", 1, 2, 1),
            row1.Single(slot => slot.SlotId == "[]" && slot.Col == 2));
        Assert.Equal(new ViewerShellSlot("[]", 1, 5, 2),
            row1.Single(slot => slot.SlotId == "[]" && slot.Col == 5));
    }

    [Fact]
    public void Content_slot_is_greedy_takes_the_cell_entirely()
    {
        var board = ParseBoard(
            "[ brand [] catalog nav help settings ]",
            "[ content ]");

        var content = Find(ShellBoardSlotResolver.Resolve(board), "content");
        Assert.Equal(2, content.Row);
        Assert.Equal(1, content.Col);
        Assert.Equal(1, content.Span);
    }

    [Fact]
    public void Content_slot_is_implicit_full_width_tail_when_not_declared()
    {
        var board = ParseBoard("[ brand [] nav ]");

        var slots = ShellBoardSlotResolver.Resolve(board);

        var content = Assert.Single(slots, slot => slot.SlotId == ViewerShellSlotIds.Content);
        Assert.Equal(2, content.Row);
        Assert.Equal(1, content.Col);
        Assert.Equal(1, content.Span);
    }

    [Fact]
    public void Spacer_only_row_fails_loud()
    {
        var board = ParseBoard(
            "[ [] [] ]",
            "[ content ]");

        var ex = Assert.Throws<DashSpecParseException>(() => ShellBoardSlotResolver.Resolve(board));
        Assert.Contains("row 1", ex.Message);
        Assert.Contains("no slots", ex.Message);
    }

    [Fact]
    public void Unknown_slot_fails_loud_with_token_and_row()
    {
        var board = ParseBoard("[ brand [] mystery ]");

        var ex = Assert.Throws<DashSpecParseException>(() => ShellBoardSlotResolver.Resolve(board));
        Assert.Contains("mystery", ex.Message);
        Assert.Contains("row 1", ex.Message);
    }

    [Fact]
    public void Legacy_aliases_normalize_to_current_slot_ids()
    {
        var board = ParseBoard("[ report_picker [] dashboard ]");

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
        // ds layout module — this pins its shape: brand + spacer + nav topbar, content below.
        var board = ExecutionLayoutModuleParser.ParseLayoutFile("""
            module layout dashspec_default_shell
            namespace DashSpec.Default.Shell
            scope host

            [ brand [] nav ]
            [ content ]
            """);

        Assert.Equal(LayoutScope.Host, board.ModuleScope.Value);

        var slots = ShellBoardSlotResolver.Resolve(board);
        Assert.Equal(4, slots.Count);
        Assert.Equal(new ViewerShellSlot("brand", 1, 1, 0), Find(slots, "brand"));
        Assert.Equal(new ViewerShellSlot("[]", 1, 2, 1), Find(slots, "[]"));
        Assert.Equal(new ViewerShellSlot("nav", 1, 3, 0), Find(slots, "nav"));
        Assert.Equal(new ViewerShellSlot("content", 2, 1, 1), Find(slots, "content"));
    }

    static ViewerShellSlot Find(IReadOnlyList<ViewerShellSlot> slots, string slotId) =>
        Assert.Single(slots, slot => slot.SlotId == slotId);
}
