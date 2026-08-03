using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

public static class PageToolbarResolver
{
    public static LayoutBoardDefinition? ResolveActiveToolbarBoard(
        DashboardDocument document,
        string? activeTabId,
        string? activePageId)
    {
        if (string.IsNullOrWhiteSpace(activePageId) || document.Pages is null)
        {
            return null;
        }

        var page = document.Pages.FirstOrDefault(p =>
            string.Equals(p.Id, activePageId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(p.TabId) ||
             string.IsNullOrWhiteSpace(activeTabId) ||
             string.Equals(p.TabId, activeTabId, StringComparison.OrdinalIgnoreCase)));

        return page?.ToolbarBoard;
    }

    public static FilterDeriveDefinition? ResolveUsageDateDerive(
        DashboardDocument document,
        string? activeTabId,
        string? activePageId)
    {
        if (string.IsNullOrWhiteSpace(activePageId) || document.Pages is null)
        {
            return null;
        }

        var page = document.Pages.FirstOrDefault(p =>
            string.Equals(p.Id, activePageId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(p.TabId) ||
             string.IsNullOrWhiteSpace(activeTabId) ||
             string.Equals(p.TabId, activeTabId, StringComparison.OrdinalIgnoreCase)));

        return page?.UsageDateDerive;
    }
}
