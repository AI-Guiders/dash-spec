#nullable enable

using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

public sealed record CommandTrailSegment(string Label, string? Canonical = null);

internal static class DashboardCommandTrailFormatter
{
    public static IReadOnlyList<CommandTrailSegment> Format(string tail, DashboardFilterContext context)
    {
        var body = DashboardFilterSlashCompletion.NormalizeBody(tail);
        if (body.Length == 0)
        {
            return [];
        }

        var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return [];
        }

        var segments = new List<CommandTrailSegment> { new(tokens[0], tokens[0]) };
        if (tokens[0].Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 2)
        {
            segments.Add(new(tokens[1], tokens[1]));
        }

        if (tokens[0].Equals(FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase)
            && tokens.Length >= 3
            && tokens[1].Equals(FilterCommandPaths.FilterBranch, StringComparison.OrdinalIgnoreCase))
        {
            var filterName = DashboardCommandEntityResolver.ResolveFilterName(tokens[2], context) ?? tokens[2];
            segments.Add(new(
                DashboardCommandEntityResolver.ResolveFilterLabel(context, filterName),
                filterName));
            if (tokens.Length >= 4)
            {
                segments.Add(new(string.Join(' ', tokens.Skip(3)), string.Join(' ', tokens.Skip(3))));
            }
        }

        if (tokens[0].Equals(ViewCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 2)
        {
            var card = DashboardCommandEntityResolver.ResolveCard(tokens[1], context);
            segments.Add(new(card?.Title ?? tokens[1], card?.CardId ?? tokens[1]));
            if (tokens.Length >= 3)
            {
                var viewLabel = card?.Views.FirstOrDefault(view =>
                    view.ViewId.Equals(tokens[2], StringComparison.OrdinalIgnoreCase))?.Label ?? tokens[2];
                segments.Add(new(viewLabel, tokens[2]));
            }
        }

        return segments;
    }
}
