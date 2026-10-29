using DashSpec.Execution.Runtime;

namespace DashSpec.Viz;

public static class ChartHeightStyle
{
    public static string Resolve(CardRenderResult card, bool detailView = false)
    {
        var baseHeight = card.ChartPresentation?.HeightPx ?? 280;
        var chart = card.ForView(detailView).Chart ?? card.Chart;

        // stretch_mode = fill (WPF Stretch vocabulary): the chart takes the slot height;
        // the resolved px height rides as the floor (up_only) / cap (down_only).
        if (card.ChartPresentation is { StretchMode: VizStretchMode.Fill } presentation)
        {
            return VizStretch.ContainerStyle(presentation.StretchMode, presentation.StretchDirection, baseHeight);
        }

        // Horizontal bars give every category its own row — Radzen's vertical category
        // axis does not skip overlapping labels (chartjs autoSkip did), so the wrap grows
        // with the category count on the board as well as in the detail view.
        if (chart is not null &&
            card.ChartPresentation?.Orientation is ChartOrientation.Horizontal &&
            chart.Labels.Count > 8)
        {
            var expanded = Math.Clamp(chart.Labels.Count * 26 + 64, baseHeight, 2400);
            return detailView
                ? $"height:{expanded}px;min-height:{baseHeight}px;"
                : $"height:{expanded}px;";
        }

        if (!detailView)
        {
            return $"height:{baseHeight}px;";
        }

        if (chart is null)
        {
            return $"height:{Math.Max(baseHeight, 420)}px;";
        }

        if (chart.Labels.Count > 24 || chart.Series.Count > 8)
        {
            return $"height:{Math.Max(baseHeight, 480)}px;";
        }

        return $"height:{Math.Max(baseHeight, 420)}px;";
    }
}
