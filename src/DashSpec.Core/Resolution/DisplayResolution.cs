using DashSpec.Core.Model;

namespace DashSpec.Core.Resolution;

/// <summary>Canonical display merge for titles and labels (ADR-0057).</summary>
public static class DisplayResolution
{
    public static ResolutionMode InferMode(string? activeCatalogEntryId, bool catalogLoaded) =>
        !string.IsNullOrWhiteSpace(activeCatalogEntryId) && catalogLoaded
            ? ResolutionMode.CatalogProd
            : ResolutionMode.SoakDev;

    public static ResolutionContext InferContext(
        DashboardDocument document,
        CatalogEntryDefinition? catalogEntry,
        TabDefinition? tab = null)
    {
        if (catalogEntry is not null)
        {
            return ResolutionContext.ForCatalog(document, catalogEntry, tab);
        }

        return ResolutionContext.ForSoak(document, tab);
    }

    public static string ResolveReportHeaderTitle(ResolutionContext context)
    {
        var document = context.Document;
        var entry = context.CatalogEntry;
        var tab = context.Tab;

        return context.Mode switch
        {
            ResolutionMode.CatalogProd when entry is not null =>
                ResolveChain(entry.Id, entry.Title, document.Title),
            ResolutionMode.DashboardEmbed =>
                ResolveChain(
                    tab?.Id ?? document.Id,
                    entry?.Title,
                    document.Title),
            _ => ResolveChain(tab?.Id ?? document.Id, document.Title),
        };
    }

    public static string ResolveTabLabel(ResolutionContext context)
    {
        var tab = context.Tab;
        var entry = context.CatalogEntry;
        var fallback = tab?.Id ?? context.Document.Id;

        return context.Mode switch
        {
            ResolutionMode.CatalogProd when entry is not null =>
                context.Document.Tabs.Count > 1
                    ? ResolveChain(fallback, tab?.Label)
                    : ResolveChain(fallback, entry.Title),
            ResolutionMode.DashboardEmbed =>
                ResolveChain(fallback, tab?.Label, entry?.Title),
            _ => ResolveChain(fallback, tab?.Label),
        };
    }

    public static string ResolvePageNavTitle(ReportPageDefinition page) =>
        ResolveChain(page.Id, page.Title);

    /// <summary>Group header text; <see langword="null"/> when <c>title</c> omitted on layout group.</summary>
    public static string? TryResolveLayoutGroupChromeTitle(LayoutBoardGroupDefinition group) =>
        string.IsNullOrWhiteSpace(group.Title) ? null : group.Title;

    public static ResolvedCardChrome ResolveCardChrome(ResolutionContext context, CardDefinition card)
    {
        var entry = context.CatalogEntry;
        var title = context.Mode switch
        {
            ResolutionMode.CatalogProd =>
                ResolveChain(card.Id, card.Title),
            ResolutionMode.DashboardEmbed =>
                ResolveChain(card.Id, entry?.Title, card.Title),
            _ => ResolveChain(card.Id, card.Title),
        };

        var suppress = card.Chrome?.HideTitle == true
                       || (context.Mode == ResolutionMode.CatalogProd
                           && entry is not null
                           && string.Equals(card.Id, entry.Id, StringComparison.OrdinalIgnoreCase));

        return suppress ? ResolvedCardChrome.Suppressed(title) : ResolvedCardChrome.Visible(title);
    }

    public static string ResolveCatalogEntryTitle(CatalogEntryDefinition entry) =>
        ResolveChain(entry.Id, entry.Title);

    public static string ResolveCatalogGroupTitle(CatalogGroupDefinition group) =>
        ResolveChain(group.Id, group.Title);

    public static string ResolveFilterLabel(FilterDefinition filter) =>
        ResolveChain(HumanizeFilterName(filter.Name), filter.Label);

    public static string ResolveGateMessage(CardVisibilityRule? visibility) =>
        string.IsNullOrWhiteSpace(visibility?.Message) ? string.Empty : visibility.Message;

    public static string ResolveDiagramAxisLabel(
        IReadOnlyDictionary<string, string> diagramProperties,
        string bindingKey,
        string fallbackBindingName)
    {
        var labelKey = $"{bindingKey}_label";
        if (diagramProperties.TryGetValue(labelKey, out var axisLabel) &&
            !string.IsNullOrWhiteSpace(axisLabel))
        {
            return axisLabel;
        }

        if (diagramProperties.TryGetValue("label", out var generic) &&
            !string.IsNullOrWhiteSpace(generic))
        {
            return generic;
        }

        return HumanizeFilterName(fallbackBindingName);
    }

    public static string HumanizeFilterName(string name) =>
        name.Replace('_', ' ');

    /// <summary>Weak → strong; rightmost non-empty wins; else <paramref name="fallback"/>.</summary>
    public static string ResolveChain(string fallback, params string?[] weakToStrong)
    {
        for (var i = weakToStrong.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(weakToStrong[i]))
            {
                return weakToStrong[i]!;
            }
        }

        return fallback;
    }
}
