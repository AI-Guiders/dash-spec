using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Viz;

namespace DashSpec.Host.Services.Presentation;

internal static class DashboardLayoutHelper
{
    public static string CardsGridStyle(LayoutDefinition layout) =>
        $"--grid-columns:{layout.Columns};--grid-gap:{layout.GapPx}px;";

    public static string CardPlacementStyle(
        CardRenderResult card,
        LayoutDefinition layout,
        IReadOnlyDictionary<string, PlacementDefinition> tabPlacements) =>
        PlacementGridStyle(ResolvePlacement(card, layout.Columns, tabPlacements), layout.Columns);

    public static string PlacementGridStyle(PlacementDefinition placement, int layoutColumns)
    {
        var span = Math.Min(placement.Span, layoutColumns);
        return placement.Row > 0
            ? $"grid-column:{placement.Col} / span {span};grid-row:{placement.Row};"
            : $"grid-column:span {span};";
    }

    public static string GroupOuterStyle(int outerRow) =>
        $"grid-column:1 / -1;grid-row:{outerRow};";

    public static string FilterPlacementStyle(
        string filterName,
        LayoutDefinition layout,
        IReadOnlyDictionary<string, PlacementDefinition> toolbarPlacements)
    {
        if (!toolbarPlacements.TryGetValue(filterName, out var placement))
        {
            return string.Empty;
        }

        var span = Math.Min(placement.Span, layout.Columns);
        return placement.Row > 0
            ? $"grid-column:{placement.Col} / span {span};grid-row:{placement.Row};"
            : $"grid-column:span {span};";
    }

    public static PlacementDefinition ResolvePlacement(
        CardRenderResult card,
        int layoutColumns,
        IReadOnlyDictionary<string, PlacementDefinition> tabPlacements)
    {
        if (tabPlacements.TryGetValue(card.Id, out var compact))
        {
            return compact;
        }

        return card.Placement ?? PlacementDefaults.ForFamily(card.DataFamily, layoutColumns);
    }

    public static bool UsesInteriorLayout(CardRenderResult card) =>
        card.InteriorPlacements is { Count: > 0 };

    public static string CardInteriorGridStyle(LayoutDefinition layout) =>
        $"--card-grid-columns:{layout.Columns};";

    public static string CardInteriorSlotStyle(
        PlacementDefinition placement,
        int columns)
    {
        var span = Math.Min(placement.Span, columns);
        return placement.Row > 0
            ? $"grid-column:{placement.Col} / span {span};grid-row:{placement.Row};"
            : $"grid-column:span {span};";
    }

    public static IReadOnlyDictionary<string, PlacementDefinition> ResolveInteriorPlacements(
        CardDefinition card,
        DashboardDocument document) =>
        CardInteriorLayoutCompactor.Compact(card, document.Filters, document.Layout.Columns);
}
