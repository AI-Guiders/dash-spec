using DashSpec.Core.Model;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class OnClickInteractionService
{
    public ShowSelectionEffect? ResolveShowEffect(CardClickBehaviour? behaviour) =>
        CardSelectionPresenter.FindShowEffect(behaviour);

    public bool HasNavigationEffects(CardClickBehaviour? behaviour) =>
        behaviour?.Effects.Any(effect => effect switch
        {
            SetFilterFromFieldEffect or GotoTabEffect => true,
            InvokeHandlerEffect invoke =>
                string.Equals(invoke.HandlerId, "drill_down", StringComparison.OrdinalIgnoreCase),
            _ => false,
        }) == true;

    public IEnumerable<CardClickEffect> ExpandClickEffects(IEnumerable<CardClickEffect> effects) =>
        effects.SelectMany(ExpandSingle);

    private static IEnumerable<CardClickEffect> ExpandSingle(CardClickEffect effect) =>
        effect is InvokeHandlerEffect invoke
            ? ExpandInvoke(invoke)
            : [effect];

    private static IEnumerable<CardClickEffect> ExpandInvoke(InvokeHandlerEffect invoke)
    {
        if (!string.Equals(invoke.HandlerId, "drill_down", StringComparison.OrdinalIgnoreCase))
        {
            yield return invoke;
            yield break;
        }

        var defaultFrom = invoke.Args.GetValueOrDefault("from") ?? "y";
        string? tabId = null;

        foreach (var (key, value) in invoke.Args)
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
                yield return new SetFilterFromFieldEffect(value, defaultFrom);
                continue;
            }

            yield return new SetFilterFromFieldEffect(key, value);
        }

        if (!string.IsNullOrWhiteSpace(tabId))
        {
            yield return new GotoTabEffect(tabId);
        }
    }
}
