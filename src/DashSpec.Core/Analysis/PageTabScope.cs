
namespace DashSpec.Core.Analysis;

public static class PageTabScope
{
    public static bool PageBelongsToTab(ReportPageDefinition page, string tabId) =>
        string.IsNullOrWhiteSpace(page.TabId?.Value) ||
        string.Equals(page.TabId?.Value, tabId, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<ReportPageDefinition> FilterForTab(
        IReadOnlyList<ReportPageDefinition>? pages,
        string tabId) =>
        (pages ?? [])
            .Where(page => PageBelongsToTab(page, tabId))
            .ToList();

    public static bool TabDeclaresPages(
        IReadOnlyList<ReportPageDefinition>? pages,
        string tabId,
        int tabCount) =>
        (pages ?? []) switch
        {
            { Count: 0 } => false,
            var list when list.Any(page =>
                string.Equals(page.TabId?.Value, tabId, StringComparison.OrdinalIgnoreCase)) => true,
            var list when tabCount == 1 &&
                          list.All(page => string.IsNullOrWhiteSpace(page.TabId?.Value)) => true,
            _ => false,
        };
}
