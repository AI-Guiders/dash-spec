using DashSpec.Core.Model;

namespace DashSpec.Host.Services.Presentation;

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
        if (card.PhaseId is not null &&
            !string.Equals(card.PhaseId, activePhaseId, StringComparison.OrdinalIgnoreCase))
        {
            return CardVisibilityOutcome.Hidden;
        }

        if (card.Visibility is null)
        {
            return CardVisibilityOutcome.Visible;
        }

        var hasValue = FilterHasSelection(card.Visibility.FilterName, selectedFields);
        return card.Visibility.Mode switch
        {
            CardVisibilityMode.WhenEmpty when !hasValue => CardVisibilityOutcome.Visible,
            CardVisibilityMode.WhenEmpty => CardVisibilityOutcome.Hidden,
            CardVisibilityMode.WhenSet when hasValue => CardVisibilityOutcome.Visible,
            CardVisibilityMode.WhenSet when !string.IsNullOrWhiteSpace(card.Visibility.Message)
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

        return card.Visibility?.Message;
    }

    public static bool FilterHasSelection(
        string filterName,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields) =>
        selectedFields.TryGetValue(filterName, out var values) &&
        values.Count > 0 &&
        values.Any(value => !string.IsNullOrWhiteSpace(value));
}
