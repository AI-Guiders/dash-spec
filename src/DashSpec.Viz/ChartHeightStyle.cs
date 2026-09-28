using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;

namespace DashSpec.Viz;

public static class ChartHeightStyle
{
    public static string Resolve(CardRenderResult card, bool detailView = false)
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
}
