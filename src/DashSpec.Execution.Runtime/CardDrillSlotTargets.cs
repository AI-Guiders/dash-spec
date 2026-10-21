using DashSpec.Modeling.Parse.Card;

namespace DashSpec.Execution.Runtime;

/// <summary>Drill-target slots of a card, with the click source that fills each (ADR-0107).</summary>
public sealed record CardDrillTargets(
    IReadOnlyDictionary<string, string> BySlot,
    bool LegacyAllSlots)
{
    /// <summary>Legacy <c>drill table from cell</c> targets every interior slot; source is a cell click.</summary>
    public bool IsTarget(string slotRef) => LegacyAllSlots || BySlot.ContainsKey(slotRef);

    public string? SourceOf(string slotRef) => BySlot.TryGetValue(slotRef, out var source)
        ? source
        : LegacyAllSlots ? "cell" : null;
}

/// <summary>
/// Resolves which interior slots are drill targets: the slots named by <c>drill table &lt;slot&gt;</c>
/// effects across the card's click handlers (plus the legacy generic behaviour). Untargeted legacy
/// drills apply to every interior slot.
/// </summary>
public static class CardDrillSlotTargets
{
    public static CardDrillTargets Resolve(CardDefinition card)
    {
        var bySlot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var legacyAllSlots = false;

        foreach (var handler in card.ClickHandlers)
        {
            foreach (var effect in handler.Effects)
            {
                if (effect.TryGetDrillBinds?.Value is null)
                {
                    continue;
                }

                var slot = effect.TryGetDrillTarget?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(slot))
                {
                    legacyAllSlots = true;
                    continue;
                }

                bySlot[slot.Trim()] = handler.Source;
            }
        }

        if (card.ClickBehaviour?.Value is { } behaviour)
        {
            foreach (var effect in behaviour.Effects)
            {
                if (effect.TryGetDrillBinds?.Value is not null &&
                    string.IsNullOrWhiteSpace(effect.TryGetDrillTarget?.Value))
                {
                    legacyAllSlots = true;
                }
            }
        }

        return new CardDrillTargets(bySlot, legacyAllSlots);
    }
}
