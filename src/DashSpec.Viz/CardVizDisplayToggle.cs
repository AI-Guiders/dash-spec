using DashSpec.Abstractions.Plugins;
using DashSpec.Execution.Runtime;

namespace DashSpec.Viz;

public static class CardVizDisplayToggle
{
    public static bool CanToggle(CardRenderResult card) =>
        TryGetMatrixPresentation(card, out _) ||
        TryGetChartPresentation(card, out _);

    public static bool TryApply(
        CardActionRequest request,
        CardRenderResult card,
        ICardVizDisplayState state)
    {
        if (!VizCardDisplayActions.TryMap(request.ActionId, out var kind))
        {
            return false;
        }

        if (TryGetMatrixPresentation(card, out var presentation))
        {
            var cardId = request.CardId;
            switch (kind)
            {
                case VizDisplayToggleKind.ValueLabels:
                {
                    var visible = VizLabelDisplayResolver.EffectiveValueLabelsVisible(
                        presentation.ValueLabels,
                        state.GetValueLabelsOverride(cardId));
                    state.ToggleValueLabels(cardId, visible);
                    return true;
                }
                case VizDisplayToggleKind.AxisLabelsX:
                {
                    var visible = VizLabelDisplayResolver.EffectiveAxisVisible(
                        presentation.AxisLabelsX,
                        state.GetAxisLabelsXOverride(cardId));
                    state.ToggleAxisLabelsX(cardId, visible);
                    return true;
                }
                case VizDisplayToggleKind.AxisLabelsY:
                {
                    var visible = VizLabelDisplayResolver.EffectiveAxisVisible(
                        presentation.AxisLabelsY,
                        state.GetAxisLabelsYOverride(cardId));
                    state.ToggleAxisLabelsY(cardId, visible);
                    return true;
                }
                case VizDisplayToggleKind.Legend:
                {
                    if (!presentation.ShowsGradientLegend)
                    {
                        return false;
                    }

                    var visible = VizLabelDisplayResolver.EffectiveLegendVisible(
                        true,
                        state.GetLegendOverride(cardId));
                    state.ToggleLegend(cardId, visible);
                    return true;
                }
                default:
                    return false;
            }
        }

        if (TryGetChartPresentation(card, out var chartPresentation))
        {
            var cardId = request.CardId;
            switch (kind)
            {
                case VizDisplayToggleKind.ValueLabels:
                {
                    var visible = state.GetValueLabelsOverride(cardId) ?? chartPresentation.Markers;
                    state.ToggleValueLabels(cardId, visible);
                    return true;
                }
                case VizDisplayToggleKind.AxisLabelsX:
                {
                    var visible = state.GetAxisLabelsXOverride(cardId) ?? chartPresentation.GridLinesX;
                    state.ToggleAxisLabelsX(cardId, visible);
                    return true;
                }
                case VizDisplayToggleKind.AxisLabelsY:
                {
                    var visible = state.GetAxisLabelsYOverride(cardId) ?? chartPresentation.GridLinesY;
                    state.ToggleAxisLabelsY(cardId, visible);
                    return true;
                }
                case VizDisplayToggleKind.Legend:
                {
                    var defaultLegend = !string.Equals(chartPresentation.Legend, "hidden", StringComparison.OrdinalIgnoreCase);
                    var visible = state.GetLegendOverride(cardId) ?? defaultLegend;
                    state.ToggleLegend(cardId, visible);
                    return true;
                }
                default:
                    return false;
            }
        }

        return false;
    }

    private static bool TryGetMatrixPresentation(CardRenderResult card, out MatrixPresentation presentation)
    {
        if (card.Matrix is null && card.DetailMatrix is null)
        {
            presentation = null!;
            return false;
        }

        if (card.MatrixPresentation is not { } matrixPresentation)
        {
            presentation = null!;
            return false;
        }

        presentation = matrixPresentation;
        return true;
    }

    private static bool TryGetChartPresentation(CardRenderResult card, out ChartPresentation presentation)
    {
        if (card.Chart is null && card.DetailChart is null)
        {
            presentation = null!;
            return false;
        }

        if (card.ChartPresentation is not { } chartPresentation)
        {
            presentation = null!;
            return false;
        }

        presentation = chartPresentation;
        return true;
    }
}
