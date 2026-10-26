using DashSpec.Modeling.Parse.Card;

namespace DashSpec.Execution.Runtime;

/// <summary>Drill-target slots of a card, with the click source(s) that fill each (ADR-0107).</summary>
public sealed record CardDrillTargets(
    IReadOnlyDictionary<string, IReadOnlyList<string>> BySlot,
    bool LegacyAllSlots)
{
    /// <summary>Legacy <c>drill table from cell</c> targets every interior slot; source is a cell click.</summary>
    public bool IsTarget(string slotRef) => LegacyAllSlots || BySlot.ContainsKey(slotRef);

    /// <summary>First click source that fills the slot — the common single-source case.</summary>
    public string? SourceOf(string slotRef) =>
        BySlot.TryGetValue(slotRef, out var sources) && sources.Count > 0
            ? sources[0]
            : LegacyAllSlots ? "cell" : null;

    /// <summary>All click sources filling the slot — a switch slot is served by several (ADR-0113).</summary>
    public IReadOnlyList<string> SourcesOf(string slotRef) =>
        BySlot.TryGetValue(slotRef, out var sources) && sources.Count > 0
            ? sources
            : LegacyAllSlots ? new[] { "cell" } : Array.Empty<string>();
}

/// <summary>
/// Resolves which interior slots are drill targets: the slots named by <c>drill table &lt;slot&gt;</c>
/// effects across the card's click handlers (plus the legacy generic behaviour). Untargeted legacy
/// drills apply to every interior slot. ADR-0113: a dotted target <c>slot.payload</c> folds into
/// its slot — one switch slot may collect several sources.
/// </summary>
public static class CardDrillSlotTargets
{
    public static CardDrillTargets Resolve(CardDefinition card)
    {
        var bySlot = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var sourceSets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var legacyAllSlots = false;

        void AddSource(string slotRef, string source)
        {
            if (!sourceSets.TryGetValue(slotRef, out var list))
            {
                list = new List<string>();
                sourceSets[slotRef] = list;
            }

            if (!list.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(source);
            }
        }

        foreach (var handler in card.ClickHandlers)
        {
            foreach (var effect in handler.Effects)
            {
                if (effect.TryGetDrillBinds?.Value is null)
                {
                    continue;
                }

                var target = (effect.TryGetDrillTarget?.Value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(target))
                {
                    legacyAllSlots = true;
                    continue;
                }

                var dot = target.IndexOf('.');
                var slotRef = dot > 0 ? target[..dot] : target;
                AddSource(slotRef, handler.Source);
                bySlot[slotRef] = sourceSets[slotRef];
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
