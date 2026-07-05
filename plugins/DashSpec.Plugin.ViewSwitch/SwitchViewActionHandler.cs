using DashSpec.Abstractions.Plugins;

namespace DashSpec.Plugin.ViewSwitch;

public sealed class SwitchViewActionHandler(ICardViewState viewState) : IDashSpecActionHandler
{
    public string ActionId => "switch_view";

    public ValueTask<DashSpecActionOutcome> ExecuteAsync(
        DashSpecActionContext context,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        if (!args.TryGetValue("view", out var viewId) || string.IsNullOrWhiteSpace(viewId))
        {
            return ValueTask.FromResult(new DashSpecActionOutcome());
        }

        viewState.SetActiveView(context.CardId, viewId);
        return ValueTask.FromResult(new DashSpecActionOutcome(
            Kind: DashSpecActionOutcomeKind.RefreshCard,
            RefreshCardId: context.CardId));
    }
}
