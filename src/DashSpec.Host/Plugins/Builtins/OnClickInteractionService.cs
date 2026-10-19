using DashSpec.Viz;
using DashSpec.Surface.Blazor.Services.Presentation;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class OnClickInteractionService : IOnClickInteractionService
{
    public ShowSelectionEffect? ResolveShowEffect(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour) =>
        CardSelectionPresenter.FindShowEffect(behaviour);

    public bool HasNavigationEffects(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour) =>
        behaviour?.Effects.Any(static effect => effect.IsNavigationEffect) == true;

    public IEnumerable<CardClickEffect> ExpandClickEffects(IEnumerable<CardClickEffect> effects) =>
        effects.SelectMany(ExpandSingle);

    private static IEnumerable<CardClickEffect> ExpandSingle(CardClickEffect effect)
    {
        if (effect.TryGetInvokeHandler?.Value is not { } invoke)
        {
            return [effect];
        }

        var handlerId = (string)invoke.Item1;
        if (!string.Equals(handlerId, "drill_down", StringComparison.OrdinalIgnoreCase))
        {
            return [effect];
        }

        return ExpandDrillDown((IReadOnlyDictionary<string, string>)invoke.Item2);
    }

    private static IEnumerable<CardClickEffect> ExpandDrillDown(IReadOnlyDictionary<string, string> args)
    {
        var defaultFrom = args.GetValueOrDefault("from") ?? "y";
        string? tabId = null;

        foreach (var (key, value) in args)
        {
            if (string.Equals(key, "from", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(key, "tab", StringComparison.OrdinalIgnoreCase))
            {
                tabId = value;
                continue;
            }

            if (string.Equals(key, "target", StringComparison.OrdinalIgnoreCase))
            {
                yield return CardClickEffect.NewSetFilterFromField(value, defaultFrom);
                continue;
            }

            yield return CardClickEffect.NewSetFilterFromField(key, value);
        }

        if (!string.IsNullOrWhiteSpace(tabId))
        {
            yield return CardClickEffect.NewGotoTab(tabId);
        }
    }
}
