#nullable enable

using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Commands;

internal static class DashboardCardCommandTargetsBuilder
{
    public static IReadOnlyList<DashboardCardCommandTarget> Build(IEnumerable<CardDefinition> cards)
    {
        var targets = new List<DashboardCardCommandTarget>();
        foreach (var card in cards)
        {
            var viewsBlock = card.ExtensionBlocks?
                .FirstOrDefault(block => string.Equals(
                    block.Keyword,
                    CardViewSwitchApplier.ViewsBlockKeyword,
                    StringComparison.OrdinalIgnoreCase));
            if (viewsBlock is null || viewsBlock.Nested.Count == 0)
            {
                continue;
            }

            var views = viewsBlock.Nested
                .Select(view => new DashboardCardViewOption(
                    view.Keyword,
                    view.Properties.GetValueOrDefault("label") ?? view.Keyword))
                .ToList();
            if (views.Count == 0)
            {
                continue;
            }

            targets.Add(new DashboardCardCommandTarget(card.Id, card.Title, views));
        }

        return targets;
    }
}
