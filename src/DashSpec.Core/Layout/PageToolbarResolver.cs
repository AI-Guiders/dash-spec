using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

public static class PageToolbarResolver
{
    public static LayoutBoardDefinition? ResolveActiveToolbarBoard(
        DashboardDocument document,
        string? activeTabId,
        string? activePageId)
    {
        if (string.IsNullOrWhiteSpace(activePageId) || document.Pages?.Value is null)
        {
            return null;
        }

        var page = document.Pages.Value.FirstOrDefault(p =>
            string.Equals(p.Id, activePageId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(p.TabId?.Value) ||
             string.IsNullOrWhiteSpace(activeTabId) ||
             string.Equals(p.TabId?.Value, activeTabId, StringComparison.OrdinalIgnoreCase)));

        return page?.ToolbarBoard?.Value;
    }

    public static FilterDeriveDefinition? ResolveUsageDateDerive(
        DashboardDocument document,
        string? activeTabId,
        string? activePageId)
    {
        if (string.IsNullOrWhiteSpace(activePageId) || document.Pages?.Value is null)
        {
            return null;
        }

        var page = document.Pages.Value.FirstOrDefault(p =>
            string.Equals(p.Id, activePageId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(p.TabId?.Value) ||
             string.IsNullOrWhiteSpace(activeTabId) ||
             string.Equals(p.TabId?.Value, activeTabId, StringComparison.OrdinalIgnoreCase)));

        return page?.UsageDateDerive?.Value;
    }
}
