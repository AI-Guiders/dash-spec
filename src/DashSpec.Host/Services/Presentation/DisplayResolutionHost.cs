using DashSpec.Core.Model;
using DashSpec.Core.Resolution;
using DashSpec.Execution.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Presentation;

/// <summary>Host wiring for <see cref="DisplayResolution"/> (ADR-0057 P1).</summary>
internal static class DisplayResolutionHost
{
    public static ResolutionContext CreateContext(
        IDashboardSession session,
        CatalogBootstrap catalog,
        string? activeTabId = null)
    {
        var document = session.Document;
        var tab = ResolveTab(document, activeTabId);
        var entry = ResolveCatalogEntry(session, catalog.Document);
        return DisplayResolution.InferContext(document, entry, tab);
    }

    public static string ResolveReportHeaderTitle(ResolutionContext context) =>
        DisplayResolution.ResolveReportHeaderTitle(context);

    public static CardRenderResult ApplyCardChrome(
        ResolutionContext context,
        CardDefinition card,
        CardRenderResult render,
        FilterDisplayContext? filterDisplay = null,
        IReadOnlyDictionary<string, string>? displayBindings = null)
    {
        var chrome = DisplayResolution.ResolveCardChrome(context, card);
        var title = filterDisplay is null
            ? chrome.Title
            : DisplayTitleResolver.Resolve(chrome.Title, displayBindings, filterDisplay) ?? chrome.Title;
        return render with
        {
            Title = title,
            ShowChromeTitle = chrome.ShowChromeTitle,
        };
    }

    public static string? ResolveDisplayTitle(
        string? template,
        FilterDisplayContext filterDisplay,
        IReadOnlyDictionary<string, string>? displayBindings = null) =>
        DisplayTitleResolver.Resolve(template, displayBindings, filterDisplay);

    public static string ResolveTabLabel(ResolutionContext context) =>
        DisplayResolution.ResolveTabLabel(context);

    public static string ResolvePageNavTitle(ReportPageDefinition page) =>
        DisplayResolution.ResolvePageNavTitle(page);

    public static string ResolveFilterLabel(FilterDefinition filter) =>
        DisplayResolution.ResolveFilterLabel(filter);

    public static string ResolveCatalogEntryTitle(CatalogEntryDefinition entry) =>
        DisplayResolution.ResolveCatalogEntryTitle(entry);

    public static string ResolveCatalogGroupTitle(CatalogGroupDefinition group) =>
        DisplayResolution.ResolveCatalogGroupTitle(group);

    private static CatalogEntryDefinition? ResolveCatalogEntry(
        IDashboardSession session,
        DashSpec.Core.Model.CatalogDocument catalogDocument)
    {
        if (string.IsNullOrWhiteSpace(session.ActiveCatalogEntryId))
        {
            return null;
        }

        return catalogDocument.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Id, session.ActiveCatalogEntryId, StringComparison.OrdinalIgnoreCase));
    }

    private static TabDefinition? ResolveTab(DashboardDocument document, string? activeTabId)
    {
        if (document.Tabs.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(activeTabId))
        {
            return document.Tabs.FirstOrDefault(tab =>
                string.Equals(tab.Id, activeTabId, StringComparison.OrdinalIgnoreCase));
        }

        return document.Tabs[0];
    }
}
