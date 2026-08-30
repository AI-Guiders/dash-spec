#nullable enable

using DashSpec.Core.Model;

namespace DashSpec.Host.Commands;

internal static class DashboardCommandEntityResolver
{
    public static string? ResolveFilterName(string token, DashboardFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var trimmed = token.Trim();
        if (context.ToolbarFilterNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return context.ToolbarFilterNames.First(name =>
                name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        if (context.CommandAliases.TryGetValue(trimmed, out var aliasTarget)
            && context.ToolbarFilterNames.Contains(aliasTarget, StringComparer.OrdinalIgnoreCase))
        {
            return aliasTarget;
        }

        foreach (var filterName in context.ToolbarFilterNames)
        {
            if (!context.FilterIndex.TryGetValue(filterName, out var filter))
            {
                continue;
            }

            if (MatchesToken(trimmed, filterName, DashboardFilterSlashLabels.ResolveFilterLabel(context, filterName)))
            {
                return filterName;
            }
        }

        return null;
    }

    public static string? ResolveCatalogEntryId(string token, DashboardFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var trimmed = token.Trim();
        var byId = context.CatalogEntries.FirstOrDefault(entry =>
            entry.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return byId.Id;
        }

        var matches = context.CatalogEntries
            .Where(entry => entry.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                            || entry.Title.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    public static string? ResolvePageId(string token, DashboardFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var trimmed = token.Trim();
        var byId = context.ReportPages.FirstOrDefault(page =>
            page.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return byId.Id;
        }

        var matches = context.ReportPages
            .Where(page => page.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                           || page.Title.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    public static DashboardCardCommandTarget? ResolveCard(string token, DashboardFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var trimmed = token.Trim();
        var byId = context.SwitchableCards.FirstOrDefault(card =>
            card.CardId.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return byId;
        }

        var matches = context.SwitchableCards
            .Where(card => card.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                           || card.Title.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public static string? ResolveViewId(DashboardCardCommandTarget card, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var trimmed = token.Trim();
        var byId = card.Views.FirstOrDefault(view =>
            view.ViewId.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byId is not null)
        {
            return byId.ViewId;
        }

        var matches = card.Views
            .Where(view => view.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                           || view.Label.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count == 1 ? matches[0].ViewId : null;
    }

    public static string ResolveFilterLabel(DashboardFilterContext context, string filterName) =>
        DashboardFilterSlashLabels.ResolveFilterLabel(context, filterName);

    static bool MatchesToken(string token, string id, string label) =>
        id.Equals(token, StringComparison.OrdinalIgnoreCase)
        || label.Equals(token, StringComparison.OrdinalIgnoreCase)
        || label.Contains(token, StringComparison.OrdinalIgnoreCase);
}
