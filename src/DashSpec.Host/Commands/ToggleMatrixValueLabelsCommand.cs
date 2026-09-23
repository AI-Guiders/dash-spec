#nullable enable

using AIGuiders.Platform.Execution.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal sealed class ToggleMatrixValueLabelsCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.card.matrix.values.toggle";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var cardId = DashCatalog.PhraseSlots.ReadBoundSlotValue(context.CanonicalPath, Id, "card")
            ?? context.ArgTail.Trim();
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return CommandOutcome.Fail("Укажите heatmap-карточку.");
        }

        var card = DashboardCommandEntityResolver.ResolveMatrixCard(cardId, context);
        if (card is null)
        {
            return CommandOutcome.Fail($"Heatmap-карточка '{cardId}' не найдена.");
        }

        context.PendingCardId = card.CardId;
        context.PendingCardActionId = "toggle_matrix_value_labels";
        return CommandOutcome.Ok();
    }
}
