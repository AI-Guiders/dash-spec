using DashSpec.Core.Model;
using DashSpec.Core.Resolution;

namespace DashSpec.Surface.Blazor.Services.Presentation;

public enum CardVisibilityOutcome
{
    Visible,
    Hidden,
    Placeholder,
}

public static class CardVisibilityEvaluator
{
    public static CardVisibilityOutcome Evaluate(
        CardDefinition card,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields,
        string? activePhaseId)
    {
        var phaseId = card.PhaseId?.Value;
        if (phaseId is not null &&
            !string.Equals(phaseId, activePhaseId, StringComparison.OrdinalIgnoreCase))
        {
            return CardVisibilityOutcome.Hidden;
        }

        var visibility = card.Visibility?.Value;
        if (visibility is null)
        {
            return CardVisibilityOutcome.Visible;
        }

        var hasValue = FilterHasSelection(visibility.FilterName, selectedFields);
        return visibility.Mode switch
        {
            CardVisibilityMode.WhenEmpty when !hasValue => CardVisibilityOutcome.Visible,
            CardVisibilityMode.WhenEmpty => CardVisibilityOutcome.Hidden,
            CardVisibilityMode.WhenSet when hasValue => CardVisibilityOutcome.Visible,
            CardVisibilityMode.WhenSet when !string.IsNullOrWhiteSpace(visibility.Message?.Value)
                => CardVisibilityOutcome.Placeholder,
            CardVisibilityMode.WhenSet => CardVisibilityOutcome.Hidden,
            _ => CardVisibilityOutcome.Visible,
        };
    }

    public static string? PlaceholderMessage(
        CardDefinition card,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields,
        string? activePhaseId)
    {
        if (Evaluate(card, selectedFields, activePhaseId) is not CardVisibilityOutcome.Placeholder)
        {
            return null;
        }

        return DisplayResolution.ResolveGateMessage(card.Visibility?.Value);
    }

    public static bool FilterHasSelection(
        string filterName,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields) =>
        selectedFields.TryGetValue(filterName, out var values) &&
        values.Count > 0 &&
        values.Any(value => !string.IsNullOrWhiteSpace(value));
}
