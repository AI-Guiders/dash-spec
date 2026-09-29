using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

public static class CardInteriorLayoutDefaults
{
    public static LayoutBoardDefinition Synthesize(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);

        // Local filters default to card-head chrome only; explicit `layout` may still place them in interior.
        var rows = new List<IReadOnlyList<string>>();
        var slotRefs = ResolveSlotsInDeclarationOrder(card);
        foreach (var slotRef in slotRefs)
        {
            var token = string.Equals(slotRef, CardInteriorSlots.Diagram, StringComparison.Ordinal)
                ? "diagram"
                : slotRef;
            rows.Add([token]);
        }

        if (slotRefs.Count == 0)
        {
            throw new DashSpecParseException($"Card '{card.Id}': card requires at least one diagram slot.");
        }

        return LayoutBoardDefinition.FromCardRows(rows);
    }

    private static IReadOnlyList<string> ResolveSlotsInDeclarationOrder(CardDefinition card)
    {
        var slots = CardDiagramSlotCatalog.ResolveSlots(card);
        if (slots.Count == 0)
        {
            return [CardInteriorSlots.Diagram];
        }

        return slots.Keys.ToList();
    }

}
