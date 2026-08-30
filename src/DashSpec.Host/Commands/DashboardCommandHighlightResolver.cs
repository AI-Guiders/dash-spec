#nullable enable

namespace DashSpec.Host.Commands;

public sealed record CommandHighlightState(
    IReadOnlySet<string> FilterNames,
    IReadOnlySet<string> CardIds)
{
    public static CommandHighlightState Empty { get; } =
        new(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

internal static class DashboardCommandHighlightResolver
{
    public static CommandHighlightState Resolve(string tail, DashboardFilterContext context)
    {
        var body = DashboardFilterSlashCompletion.SanitizeLine(tail).Trim();
        if (body.Length == 0)
        {
            return AllTargets(context);
        }

        var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return AllTargets(context);
        }

        if (tokens[0].Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSelect(tokens, context);
        }

        if (tokens[0].Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveView(tokens, context);
        }

        return AllTargets(context);
    }

    static CommandHighlightState AllTargets(DashboardFilterContext context)
    {
        var filters = context.ToolbarFilterNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cards = context.SwitchableCards
            .Select(card => card.CardId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new CommandHighlightState(filters, cards);
    }

    static CommandHighlightState ResolveSelect(string[] tokens, DashboardFilterContext context)
    {
        if (tokens.Length < 2
            || !tokens[1].Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase))
        {
            return new CommandHighlightState(
                context.ToolbarFilterNames.ToHashSet(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        if (tokens.Length < 3)
        {
            return new CommandHighlightState(
                context.ToolbarFilterNames.ToHashSet(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var resolved = DashboardCommandEntityResolver.ResolveFilterName(tokens[2], context);
        if (resolved is null)
        {
            var partialMatches = context.ToolbarFilterNames
                .Where(name => MatchesPartial(name, tokens[2])
                               || MatchesPartial(DashboardCommandEntityResolver.ResolveFilterLabel(context, name), tokens[2]))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new CommandHighlightState(partialMatches, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        return new CommandHighlightState(
            new HashSet<string>([resolved], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    static CommandHighlightState ResolveView(string[] tokens, DashboardFilterContext context)
    {
        if (tokens.Length < 2)
        {
            return new CommandHighlightState(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                context.SwitchableCards.Select(card => card.CardId).ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        var card = DashboardCommandEntityResolver.ResolveCard(tokens[1], context);
        if (card is null)
        {
            var partialCards = context.SwitchableCards
                .Where(cardTarget => MatchesPartial(cardTarget.CardId, tokens[1])
                                     || MatchesPartial(cardTarget.Title, tokens[1]))
                .Select(cardTarget => cardTarget.CardId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return new CommandHighlightState(new HashSet<string>(StringComparer.OrdinalIgnoreCase), partialCards);
        }

        return new CommandHighlightState(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>([card.CardId], StringComparer.OrdinalIgnoreCase));
    }

    static bool MatchesPartial(string value, string partial) =>
        partial.Length == 0 || value.StartsWith(partial, StringComparison.OrdinalIgnoreCase);
}
