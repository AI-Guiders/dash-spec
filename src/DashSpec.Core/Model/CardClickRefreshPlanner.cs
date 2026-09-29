namespace DashSpec.Core.Model;

/// <summary>
/// SSOT: what re-queries after <c>on click</c>, derived from parsed spec effects (ADR-0028).
/// Authors declare effects in .dashspec only; Host reads this planner.
/// </summary>
public enum CardInteractionRefresh
{
    /// <summary>No server re-query (e.g. show below only).</summary>
    None,

    /// <summary>Dashboard <c>FilterState</c> changed — refresh cards bound to those filters.</summary>
    Report,

    /// <summary>Cell binds scoped to this card's non-primary <c>data for …</c> slots only.</summary>
    CardInteriorSlots,
}

public static class CardClickRefreshPlanner
{
    public static CardInteractionRefresh Plan(IReadOnlyList<CardClickEffect> effects)
    {
        if (effects.Any(static e => e is DrillTableFromCellEffect))
        {
            return CardInteractionRefresh.CardInteriorSlots;
        }

        if (effects.Any(static e => e switch
            {
                SetFilterFromFieldEffect => true,
                GotoTabEffect => true,
                GotoPageEffect => true,
                FocusPhaseEffect => true,
                GotoCatalogEntryEffect => true,
                _ => false,
            }))
        {
            return CardInteractionRefresh.Report;
        }

        return CardInteractionRefresh.None;
    }
}
