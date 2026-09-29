using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;

namespace DashSpec.Viz;

public static class CardVizDisplayToggle
{
    public static bool CanToggle(CardRenderResult card) =>
        TryGetPresentation(card, out _);

    public static bool TryApply(
        CardActionRequest request,
        CardRenderResult card,
        ICardVizDisplayState state)
    {
        if (!VizCardDisplayActions.TryMap(request.ActionId, out var kind))
        {
            return false;
        }

        if (!TryGetPresentation(card, out var presentation))
        {
            return false;
        }

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

    private static bool TryGetPresentation(CardRenderResult card, out MatrixPresentation presentation)
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
}
