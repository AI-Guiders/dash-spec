using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Ordered chrome slot ids (filters + apply) for card-head grid rendering.</summary>
public static class CardFilterChromeSlotOrder
{
    public static IEnumerable<string> Order(
        IReadOnlyList<string> localFilters,
        IReadOnlyDictionary<string, PlacementDefinition>? chromePlacements,
        int applySplitIndex,
        bool showApplyButton)
    {
        if (chromePlacements is { Count: > 0 })
        {
            var slotIds = new List<string>();
            foreach (var filterName in localFilters)
            {
                if (chromePlacements.ContainsKey(filterName))
                {
                    slotIds.Add(filterName);
                }
            }

            if (showApplyButton &&
                chromePlacements.ContainsKey(CardLocalFilterChromeCompactor.ApplySlotId))
            {
                slotIds.Add(CardLocalFilterChromeCompactor.ApplySlotId);
            }

            foreach (var slotId in slotIds.OrderBy(id => chromePlacements[id].Col))
            {
                yield return slotId;
            }

            yield break;
        }

        var before = localFilters.Take(applySplitIndex);
        var after = localFilters.Skip(applySplitIndex);
        foreach (var name in before)
        {
            yield return name;
        }

        if (showApplyButton)
        {
            yield return CardLocalFilterChromeCompactor.ApplySlotId;
        }

        foreach (var name in after)
        {
            yield return name;
        }
    }

    public static int GridColumnCount(IReadOnlyDictionary<string, PlacementDefinition> chromePlacements) =>
        chromePlacements.Values.Max(static p => p.Col + p.Span - 1);
}
