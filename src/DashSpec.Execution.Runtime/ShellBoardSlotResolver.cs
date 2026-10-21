using DashSpec.Abstractions.Viewer;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Shell grid slots ([ADR-0111] §2): the scope-host layout board from the host module is the
/// shell grid itself — chrome slots plus the content slot, placed as recorded in ds. This is
/// the correspondence piece: unknown slot tokens fail loud; the content slot, when not
/// declared, implicitly takes the next row at full width (a recorded default).
/// </summary>
public static class ShellBoardSlotResolver
{
    private static readonly HashSet<string> KnownSlotIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ViewerShellSlotIds.Brand,
        ViewerShellSlotIds.Catalog,
        ViewerShellSlotIds.ReportPicker,
        ViewerShellSlotIds.Nav,
        ViewerShellSlotIds.Dashboard,
        ViewerShellSlotIds.ExternalLinks,
        ViewerShellSlotIds.Help,
        ViewerShellSlotIds.Settings,
        ViewerShellSlotIds.SpecDev,
        ViewerShellSlotIds.Content,
    };

    /// <summary>
    /// Resolve the shell grid slots from the host module board; null/empty board is the
    /// caller's fallback to resolve (the embedded default in the host). Shell rows read as
    /// cells ([ADR-0111] §2): chrome slots fit their content (Span 0), a `[]` empty cell is
    /// a rubber spacer sharing the row's free space by weight, and the content slot is
    /// greedy (Span 1) — it takes the cell entirely, as recorded.
    /// </summary>
    public static IReadOnlyList<ViewerShellSlot> Resolve(LayoutBoardDefinition? board)
    {
        if (board is null || !board.Rows.Any())
        {
            throw new DashSpecParseException(
                "Shell board is required: pass the host module board or the embedded default.");
        }

        var slots = new List<ViewerShellSlot>();
        var rowIdx = 0;
        foreach (var row in board.Rows)
        {
            rowIdx++;
            var colIdx = 0;
            var rowHasSlots = false;
            foreach (var token in row)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                var (cellId, weight) = LayoutBoardCellParser.parseCell(token, "Shell board", rowIdx);
                colIdx++;

                if (cellId == "[]")
                {
                    slots.Add(new ViewerShellSlot("[]", rowIdx, colIdx, weight));
                    continue;
                }

                var normalized = Normalize(cellId);
                if (!KnownSlotIds.Contains(normalized))
                {
                    throw new DashSpecParseException(
                        $"Shell board slot '{token}' is not a known shell slot (row {rowIdx}).");
                }

                rowHasSlots = true;
                slots.Add(new ViewerShellSlot(normalized, rowIdx, colIdx, SpanFor(normalized)));
            }

            if (!rowHasSlots)
            {
                throw new DashSpecParseException(
                    $"Shell board row {rowIdx} declares no slots: spacers alone carry no chrome.");
            }
        }

        if (!slots.Any(slot => slot.SlotId == ViewerShellSlotIds.Content))
        {
            slots.Add(new ViewerShellSlot(ViewerShellSlotIds.Content, rowIdx + 1, 1, 1));
        }

        return slots;
    }

    /// <summary>Span semantics: 0 — fit content (chrome); 1 — greedy, shares free space (content slot).</summary>
    static int SpanFor(string slotId) =>
        slotId == ViewerShellSlotIds.Content ? 1 : 0;

    /// <summary>Legacy aliases: report_picker → catalog, dashboard → nav (ADR-0054 compat).</summary>
    public static string Normalize(string token)
    {
        var value = token.Trim();
        if (value.Equals(ViewerShellSlotIds.ReportPicker, StringComparison.OrdinalIgnoreCase))
        {
            return ViewerShellSlotIds.Catalog;
        }

        if (value.Equals(ViewerShellSlotIds.Dashboard, StringComparison.OrdinalIgnoreCase))
        {
            return ViewerShellSlotIds.Nav;
        }

        return value.ToLowerInvariant();
    }
}
