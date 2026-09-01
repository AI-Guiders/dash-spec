#nullable enable

namespace DashSpec.Host.Commands;

/// <summary>Planet noun resolution for catalog phrase slots (GUIDERS-ADR-0053 / 0054).</summary>
internal static class DashboardSlotNouns
{
    public static (string Primary, string? Secondary) FormatValue(
        string slotName,
        string slotValue,
        string typedBody,
        string? commandId,
        DashboardFilterContext context)
    {
        if (slotName.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            var card = context.SwitchableCards.FirstOrDefault(target =>
                target.CardId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return card is not null ? (card.Title, card.CardId) : (slotValue, slotValue);
        }

        if (slotName.Equals("view", StringComparison.OrdinalIgnoreCase))
        {
            var cardId = DashboardCatalog.PhraseSlots.ReadBoundSlotValue(typedBody, commandId, "card");
            var card = context.SwitchableCards.FirstOrDefault(target =>
                cardId is not null
                && target.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            var view = card?.Views.FirstOrDefault(option =>
                option.ViewId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return view is not null ? ($"{card!.Title} — {view.Label}", view.ViewId) : (slotValue, slotValue);
        }

        if (slotName.Equals("surface", StringComparison.OrdinalIgnoreCase))
        {
            var surface = HostSurfaceCatalog.Surfaces.FirstOrDefault(entry =>
                entry.Id.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return surface is not null ? (surface.Title, surface.Id) : (slotValue, slotValue);
        }

        return (slotValue, slotValue);
    }

    public static string FormatHelp(
        string slotName,
        string slotValue,
        string typedBody,
        string? commandId,
        DashboardFilterContext context)
    {
        if (slotName.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            var card = context.SwitchableCards.FirstOrDefault(target =>
                target.CardId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return card is null
                ? slotValue
                : string.Join(" · ", card.Views.Select(view => view.Label));
        }

        if (slotName.Equals("view", StringComparison.OrdinalIgnoreCase))
        {
            var cardId = DashboardCatalog.PhraseSlots.ReadBoundSlotValue(typedBody, commandId, "card");
            var card = context.SwitchableCards.FirstOrDefault(target =>
                cardId is not null
                && target.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            var view = card?.Views.FirstOrDefault(option =>
                option.ViewId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return view?.Label ?? slotValue;
        }

        return slotValue;
    }
}
