using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

internal static class CardLayoutRefResolver
{
    public static string Resolve(string token, IReadOnlyList<CardDefinition> cards, string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string? byRef = null;
        string? byId = null;

        foreach (var card in cards)
        {
            if (!string.IsNullOrWhiteSpace(card.LayoutRef) &&
                string.Equals(card.LayoutRef, token, StringComparison.OrdinalIgnoreCase))
            {
                if (byRef is not null)
                {
                    throw new DashSpecParseException(
                        $"{context}: layout token '{token}' matches more than one card ref.");
                }

                byRef = card.Id;
            }

            if (string.Equals(card.Id, token, StringComparison.OrdinalIgnoreCase))
            {
                if (byId is not null)
                {
                    throw new DashSpecParseException(
                        $"{context}: layout token '{token}' matches more than one card id.");
                }

                byId = card.Id;
            }
        }

        if (byRef is not null && byId is not null && !string.Equals(byRef, byId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"{context}: layout token '{token}' is ambiguous (matches both ref and id).");
        }

        var resolved = byRef ?? byId;
        if (resolved is null)
        {
            throw new DashSpecParseException(
                $"{context}: layout token '{token}' does not match any card ref or id on the tab.");
        }

        return resolved;
    }
}
