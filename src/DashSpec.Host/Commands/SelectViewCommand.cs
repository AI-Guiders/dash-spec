#nullable enable

using AIGuiders.Platform.Execution.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal sealed class SelectViewCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.card.view";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var slots = DashboardCatalog.PhraseSlots;
        var cardId = slots.ReadBoundSlotValue(context.CanonicalPath, Id, "card");
        var viewId = slots.ReadBoundSlotValue(context.CanonicalPath, Id, "view") ?? context.ArgTail.Trim();
        if (string.IsNullOrWhiteSpace(cardId) || string.IsNullOrWhiteSpace(viewId))
        {
            return CommandOutcome.Fail("Укажите карточку и представление.");
        }

        var card = context.SwitchableCards.FirstOrDefault(target =>
            target.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            return CommandOutcome.Fail($"Карточка '{cardId}' не найдена.");
        }

        var resolvedView = DashboardCommandEntityResolver.ResolveViewId(card, viewId);
        if (resolvedView is null)
        {
            return CommandOutcome.Fail($"Представление '{viewId}' недоступно для карточки.");
        }

        context.PendingCardId = card.CardId;
        context.PendingViewId = resolvedView;
        return CommandOutcome.Ok();
    }
}
