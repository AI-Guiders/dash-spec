#nullable enable

using AIGuiders.Platform.Authoring.Command.Catalog;
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>Typed completion labels from <c>dash.catalog</c> phrase + fills (GUIDERS-ADR-0047).</summary>
internal static class DashboardCatalogCompletion
{
    public const string CardViewCommand = "card.view";
    public const string ShowHostCommand = "host.show";

    public static string? ResolveActiveSlot(string typedBody, string commandId)
    {
        var row = FindCommand(commandId);
        if (row is null)
        {
            return null;
        }

        var fills = ParseFills(row);
        if (fills.Count == 0)
        {
            return null;
        }

        if (!row.Columns.TryGetValue("phrase", out var phraseName) || string.IsNullOrWhiteSpace(phraseName))
        {
            return null;
        }

        var literalPrefix = ReadPhraseLiteralPrefix(phraseName);
        var body = DashboardFilterSlashCompletion.SanitizeLine(typedBody);
        if (!body.StartsWith(literalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var boundSlotCount = CountBoundSlots(body, literalPrefix);
        if (boundSlotCount >= fills.Count)
        {
            return null;
        }

        return fills[boundSlotCount];
    }

    public static string? ReadBoundSlotValue(string typedBody, string commandId, string slotName)
    {
        var row = FindCommand(commandId);
        if (row is null)
        {
            return null;
        }

        var fills = ParseFills(row);
        var slotIndex = IndexOfFill(fills, slotName);
        if (slotIndex < 0)
        {
            return null;
        }

        if (!row.Columns.TryGetValue("phrase", out var phraseName) || string.IsNullOrWhiteSpace(phraseName))
        {
            return null;
        }

        var literalPrefix = ReadPhraseLiteralPrefix(phraseName);
        var tokens = ReadBoundTokens(DashboardFilterSlashCompletion.SanitizeLine(typedBody), literalPrefix);
        return slotIndex < tokens.Count ? tokens[slotIndex] : null;
    }

    public static (string Primary, string? Secondary) FormatSlotValue(
        string commandId,
        string slotName,
        string slotValue,
        string typedBody,
        DashboardFilterContext context)
    {
        if (string.Equals(commandId, CardViewCommand, StringComparison.OrdinalIgnoreCase))
        {
            return FormatCardViewSlot(slotName, slotValue, typedBody, context);
        }

        if (string.Equals(commandId, ShowHostCommand, StringComparison.OrdinalIgnoreCase)
            && slotName.Equals("surface", StringComparison.OrdinalIgnoreCase))
        {
            var surface = HostSurfaceCatalog.Surfaces.FirstOrDefault(entry =>
                entry.Id.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return surface is not null ? (surface.Title, surface.Id) : (slotValue, slotValue);
        }

        return (slotValue, slotValue);
    }

    public static string FormatSlotHelp(
        string commandId,
        string slotName,
        string slotValue,
        string typedBody,
        DashboardFilterContext context)
    {
        if (!string.Equals(commandId, CardViewCommand, StringComparison.OrdinalIgnoreCase))
        {
            return slotValue;
        }

        if (slotName.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            var card = context.SwitchableCards.FirstOrDefault(target =>
                target.CardId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return card is null
                ? slotValue
                : string.Join(" · ", card.Views.Select(view => view.Label));
        }

        if (slotName.Equals("view", StringComparison.OrdinalIgnoreCase))
        {
            var cardId = ReadBoundSlotValue(typedBody, CardViewCommand, "card");
            var card = context.SwitchableCards.FirstOrDefault(target =>
                cardId is not null
                && target.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            var view = card?.Views.FirstOrDefault(option =>
                option.ViewId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return view?.Label ?? slotValue;
        }

        return slotValue;
    }

    public static bool TryFormatPhraseSlotSuggestion(
        string typedBody,
        string commandId,
        ArgCompletionItem item,
        DashboardFilterContext context,
        out (string Primary, string? Secondary) parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(item.StepSegment))
        {
            return false;
        }

        var activeSlot = ResolveActiveSlot(typedBody, commandId);
        if (string.IsNullOrWhiteSpace(activeSlot))
        {
            return false;
        }

        parts = FormatSlotValue(commandId, activeSlot, item.StepSegment, typedBody, context);
        return true;
    }

    static (string Primary, string? Secondary) FormatCardViewSlot(
        string slotName,
        string slotValue,
        string typedBody,
        DashboardFilterContext context)
    {
        if (slotName.Equals("card", StringComparison.OrdinalIgnoreCase))
        {
            var card = context.SwitchableCards.FirstOrDefault(target =>
                target.CardId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return card is not null ? (card.Title, card.CardId) : (slotValue, slotValue);
        }

        if (slotName.Equals("view", StringComparison.OrdinalIgnoreCase))
        {
            var cardId = ReadBoundSlotValue(typedBody, CardViewCommand, "card");
            var card = context.SwitchableCards.FirstOrDefault(target =>
                cardId is not null
                && target.CardId.Equals(cardId, StringComparison.OrdinalIgnoreCase));
            var view = card?.Views.FirstOrDefault(option =>
                option.ViewId.Equals(slotValue, StringComparison.OrdinalIgnoreCase));
            return view is not null ? ($"{card!.Title} — {view.Label}", view.ViewId) : (slotValue, slotValue);
        }

        return (slotValue, slotValue);
    }

    static CatalogCommandRow? FindCommand(string commandId) =>
        DashboardCatalog.Current.Commands.FirstOrDefault(row =>
            string.Equals(row.Command, commandId, StringComparison.OrdinalIgnoreCase));

    static IReadOnlyList<string> ParseFills(CatalogCommandRow row) =>
        row.Columns.TryGetValue("fills", out var fills) && !string.IsNullOrWhiteSpace(fills)
            ? fills.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [];

    static string ReadPhraseLiteralPrefix(string phraseName)
    {
        var template = DashboardCatalogPhrases.ResolvePhrase(phraseName);
        var slotStart = template.IndexOf('{');
        return slotStart < 0 ? template.Trim() : template[..slotStart].TrimEnd();
    }

    static int CountBoundSlots(string body, string literalPrefix)
    {
        var tail = body[literalPrefix.Length..].TrimStart();
        return tail.Length == 0
            ? 0
            : tail.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    static IReadOnlyList<string> ReadBoundTokens(string body, string literalPrefix)
    {
        if (!body.StartsWith(literalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var tail = body[literalPrefix.Length..].TrimStart();
        return tail.Length == 0
            ? []
            : tail.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    static int IndexOfFill(IReadOnlyList<string> fills, string slotName)
    {
        for (var i = 0; i < fills.Count; i++)
        {
            if (fills[i].Equals(slotName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
