using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

public enum LayoutBoardRefKind
{
    Card,
    Nest,
}

public readonly record struct LayoutBoardRefTarget(LayoutBoardRefKind Kind, string Id);

/// <summary>Resolves bracket cell tokens to a card or nest (ADR-0063).</summary>
public static class LayoutBoardRefResolver
{
    public static LayoutBoardRefTarget Resolve(
        string token,
        IReadOnlyList<CardDefinition> cards,
        IReadOnlyDictionary<string, LayoutBoardNestDefinition> nests,
        string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        foreach (var (nestId, _) in nests)
        {
            if (string.Equals(nestId, token, StringComparison.OrdinalIgnoreCase))
            {
                return new LayoutBoardRefTarget(LayoutBoardRefKind.Nest, nestId);
            }
        }

        var cardId = CardLayoutRefResolver.Resolve(token, cards, context);
        return new LayoutBoardRefTarget(LayoutBoardRefKind.Card, cardId);
    }
}
