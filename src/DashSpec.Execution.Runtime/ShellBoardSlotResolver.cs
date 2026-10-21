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
    /// <summary>Shell grid columns — the same default as <see cref="LayoutDefinition.Default"/>.</summary>
    public const int GridColumns = 12;

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
    /// caller's fallback to resolve (the embedded default in the host). Rows share the
    /// full width by weight — the same math as the cards' boards (<see cref="LayoutBoardRowPlacer"/>,
    /// ADR-0020/0061): single-cell rows span all columns, multi-cell rows split proportionally
    /// with the last cell taking the remainder — a shell row never leaves the width unused.
    /// </summary>
    public static IReadOnlyList<ViewerShellSlot> Resolve(LayoutBoardDefinition? board)
    {
        if (board is null || !board.Rows.Any())
        {
            throw new DashSpecParseException(
                "Shell board is required: pass the host module board or the embedded default.");
        }

        var placements = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var rowIdx = 0;
        foreach (var row in board.Rows)
        {
            rowIdx++;
            LayoutBoardRowPlacer.PlaceRow(
                row,
                rowIdx,
                GridColumns,
                "Shell board",
                token =>
                {
                    var (slotId, _) = LayoutBoardCellParser.parseCell(token, "Shell board", rowIdx);
                    var normalized = Normalize(slotId);
                    if (!KnownSlotIds.Contains(normalized))
                    {
                        throw new DashSpecParseException(
                            $"Shell board slot '{token}' is not a known shell slot (row {rowIdx}).");
                    }

                    return normalized;
                },
                placements);
        }

        var slots = placements
            .Select(pair => new ViewerShellSlot(pair.Key, pair.Value.Row, pair.Value.Col, pair.Value.Span))
            .ToList();

        if (!slots.Any(slot => slot.SlotId == ViewerShellSlotIds.Content))
        {
            slots.Add(new ViewerShellSlot(ViewerShellSlotIds.Content, rowIdx + 1, 1, GridColumns));
        }

        return slots;
    }

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
