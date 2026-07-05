using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Presentation;

internal static class DashboardLayoutHelper
{
    public static string ChartDomId(string cardId) =>
        "chart-" + DomIdHash(cardId);

    public static string MatrixHostDomId(string cardId) =>
        "matrix-host-" + DomIdHash(cardId);

    public static string MatrixCanvasDomId(string cardId) =>
        "matrix-canvas-" + DomIdHash(cardId);

    private static string DomIdHash(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)))[..12];

    public static string CardsGridStyle(LayoutDefinition layout) =>
        $"--grid-columns:{layout.Columns};--grid-gap:{layout.GapPx}px;";

    public static string CardPlacementStyle(
        CardRenderResult card,
        LayoutDefinition layout,
        IReadOnlyDictionary<string, PlacementDefinition> tabPlacements)
    {
        var placement = ResolvePlacement(card, layout.Columns, tabPlacements);
        var span = Math.Min(placement.Span, layout.Columns);
        return placement.Row > 0
            ? $"grid-column:{placement.Col} / span {span};grid-row:{placement.Row};"
            : $"grid-column:span {span};";
    }

    public static string ChartHeightStyle(CardRenderResult card) =>
        $"height:{card.ChartPresentation?.HeightPx ?? 280}px;";

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
