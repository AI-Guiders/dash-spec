using DashSpec.Core.Interaction;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Click wiring SSOT (ADR-0109): every `set filter=x|y|value` bind (sourced handlers
/// and the legacy generic behaviour) must reference a declared filter. An unknown
/// bind used to fail silently — the overlay skipped it and the drill rendered
/// unfiltered and titleless.
/// </summary>
internal static class ClickBindingAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var known = new HashSet<string>(
            document.Filters.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var card in document.Cards)
        {
            IEnumerable<CardClickEffect> effects = [];
            if (card.ClickBehaviour?.Value is { } behaviour)
            {
                effects = effects.Concat(behaviour.Effects);
            }

            foreach (var handler in card.ClickHandlers)
            {
                effects = effects.Concat(handler.Effects);
            }

            foreach (var name in BindNames(effects))
            {
                if (!known.Contains(name))
                {
                    throw new DashSpecParseException(
                        $"Card '{card.Id}': click binds unknown filter '{name}' — declare the filter or fix the bind.");
                }
            }
        }
    }

    private static IEnumerable<string> BindNames(IEnumerable<CardClickEffect> effects)
    {
        foreach (var effect in effects)
        {
            if (effect.TryGetFilterBind?.Value is { } bind)
            {
                yield return bind.Item1;
            }

            if (effect.TryGetDrillBinds?.Value is { } drillBinds)
            {
                foreach (var drillBind in drillBinds)
                {
                    yield return drillBind.Item1;
                }
            }
        }
    }
}
