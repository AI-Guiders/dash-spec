using DashSpec.Core.Model;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Presentation;

internal static class DashboardLayoutHelper
{
    public static string ChartDomId(string cardId) =>
        "chart-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(cardId)))[..12];

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

    public static PlacementDefinition ResolvePlacement(
        CardRenderResult card,
        int layoutColumns,
        IReadOnlyDictionary<string, PlacementDefinition> tabPlacements)
    {
        if (tabPlacements.TryGetValue(card.Id, out var compact))
        {
            return compact;
        }

        return card.Placement ?? DefaultPlacement(card.DataFamily, layoutColumns);
    }

    public static PlacementDefinition DefaultPlacement(DiagramDataFamily family, int columns) =>
        family is DiagramDataFamily.Table or DiagramDataFamily.Matrix
            ? new PlacementDefinition(Row: 1, Col: 1, Span: columns)
            : new PlacementDefinition(Span: columns / 2);
}
