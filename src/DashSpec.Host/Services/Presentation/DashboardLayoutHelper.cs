using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Presentation;

internal static class DashboardLayoutHelper
{
    public static string ChartDomId(string cardId, bool detailView = false) =>
        "chart-" + DomIdHash(detailView ? cardId + ":detail" : cardId);

    public static string MatrixHostDomId(string cardId, bool detailView = false) =>
        "matrix-host-" + DomIdHash(detailView ? cardId + ":detail" : cardId);

    public static string MatrixCanvasDomId(string cardId, bool detailView = false) =>
        "matrix-canvas-" + DomIdHash(detailView ? cardId + ":detail" : cardId);

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

    public static string ChartHeightStyle(CardRenderResult card, bool detailView = false)
    {
        var baseHeight = card.ChartPresentation?.HeightPx ?? 280;
        if (!detailView)
        {
            return $"height:{baseHeight}px;";
        }

        var chart = card.ForView(detailView).Chart ?? card.Chart;
        if (chart is null)
        {
            return $"height:{Math.Max(baseHeight, 420)}px;";
        }

        var categoryCount = chart.Labels.Count;
        var isHorizontal = card.ChartPresentation?.Orientation is ChartOrientation.Horizontal;

        if (isHorizontal && categoryCount > 0)
        {
            var expanded = Math.Clamp(categoryCount * 26 + 64, baseHeight, 2400);
            return $"height:{expanded}px;min-height:{baseHeight}px;";
        }

        if (categoryCount > 24 || chart.Series.Count > 8)
        {
            return $"height:{Math.Max(baseHeight, 480)}px;";
        }

        return $"height:{Math.Max(baseHeight, 420)}px;";
    }

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
