using DashSpec.Core.Analysis;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DashboardComposer
{
    public static DashboardDocument Parse(string text, string? specDirectory = null) =>
        Parse(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument Parse(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (!DocumentModuleParser.IsBlockModuleFormat(text))
        {
            throw new DashSpecParseException(
                "DashSpec requires block module format: @dashboard id { … }, @tab id { … }, or @tab id with end-block body. " +
                "See ADR-0024 and ADR-0036.");
        }

        return DocumentModuleParser.ParseDocument(text, specDirectory, parseOptions);
    }

    public static bool IsTabRootDocument(string text) =>
        DocumentModuleParser.IsBlockModuleFormat(text) &&
        IsTabBlockRoot(text);

    private static bool IsTabBlockRoot(string text)
    {
        var reader = ParserUtilities.CreateReader(text);
        reader.SkipNewlines();
        if (!reader.IsAt(TokenKind.At))
        {
            return false;
        }

        reader.Advance();
        return reader.TryKeyword("tab");
    }

    internal static DashboardDocument MergeTabModules(
        DashboardDocument document,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var filters = document.Filters.ToList();
        var dashboardFilters = document.DashboardFilters.ToList();
        var cards = document.Cards.ToList();
        var mergedTabs = new List<TabDefinition>();
        var pages = (document.Pages ?? []).ToList();
        var moduleDiagrams = new Dictionary<string, ModuleDiagramDefinition>(
            document.ResolvedModuleDiagrams,
            StringComparer.OrdinalIgnoreCase);
        var moduleChartChromePresets = new Dictionary<string, PresentationBlock>(
            document.ResolvedChartChromePresets,
            StringComparer.OrdinalIgnoreCase);

        foreach (var tab in document.Tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.DashspecPath))
            {
                mergedTabs.Add(tab);
                continue;
            }

            var modulePath = Path.GetFullPath(Path.Combine(specDirectory, tab.DashspecPath));
            if (!File.Exists(modulePath))
            {
                throw new FileNotFoundException(
                    $"Tab '{tab.Id}' dashspec not found: '{tab.DashspecPath}' (resolved: {modulePath}).",
                    modulePath);
            }

            var moduleText = File.ReadAllText(modulePath);
            if (!DocumentModuleParser.IsBlockModuleFormat(moduleText))
            {
                throw new DashSpecParseException(
                    $"Tab module '{tab.DashspecPath}' must use block format (@tab id {{ … }} or end-block @tab id).");
            }

            var module = DocumentModuleParser.ParseTabEmbedded(moduleText, tab.Id, specDirectory, filters, parseOptions);
            foreach (var filter in module.Filters)
            {
                if (filters.Any(f => string.Equals(f.Name, filter.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares filter '{filter.Name}' already on parent dashboard.");
                }

                filters.Add(filter);
                if (filter.Kind is not FilterKind.Top &&
                    !dashboardFilters.Contains(filter.Name, StringComparer.OrdinalIgnoreCase))
                {
                    InsertTabModuleDashboardFilter(dashboardFilters, filter.Name);
                }
            }

            foreach (var card in module.Cards)
            {
                if (cards.Any(c => string.Equals(c.Id, card.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares card '{card.Id}' already on parent dashboard.");
                }

                cards.Add(card);
            }

            foreach (var page in module.Pages ?? [])
            {
                var pageWithTab = string.IsNullOrWhiteSpace(page.TabId)
                    ? page with { TabId = tab.Id }
                    : page;
                if (pages.Any(p =>
                        string.Equals(p.Id, pageWithTab.Id, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.TabId ?? "", pageWithTab.TabId ?? "", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares page '{pageWithTab.Id}' already on parent dashboard.");
                }

                pages.Add(pageWithTab);
            }

            foreach (var (diagramId, definition) in module.ModuleDiagrams ?? DashboardDocument.EmptyModuleDiagrams)
            {
                if (moduleDiagrams.ContainsKey(diagramId))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares module diagram preset '{diagramId}'.");
                }

                moduleDiagrams[diagramId] = definition;
            }

            foreach (var (presetId, preset) in module.ModuleChartChromePresets ?? DashboardDocument.EmptyModuleChartChromePresets)
            {
                if (moduleChartChromePresets.ContainsKey(presetId))
                {
                    throw new DashSpecParseException(
                        $"Tab module '{tab.Id}' redeclares chart chrome preset '{presetId}'.");
                }

                moduleChartChromePresets[presetId] = preset;
            }

            var label = tab.Label ?? module.Label;
            mergedTabs.Add(new TabDefinition(
                tab.Id,
                label,
                module.Cards.Select(c => c.Id).ToList(),
                LayoutBoard: module.LayoutBoard));
        }

        cards = TabParser.AssignTabs(cards, mergedTabs);

        var merged = document with
        {
            Filters = filters,
            DashboardFilters = dashboardFilters,
            Cards = cards,
            Tabs = mergedTabs,
            ModuleDiagrams = moduleDiagrams,
            ModuleChartChromePresets = moduleChartChromePresets,
            Pages = pages,
        };

        DashboardValidator.Validate(merged);
        return merged;
    }

    private static void InsertTabModuleDashboardFilter(List<string> dashboardFilters, string filterName)
    {
        if (string.Equals(filterName, "period_start", StringComparison.OrdinalIgnoreCase))
        {
            var grainIndex = dashboardFilters.FindIndex(name =>
                string.Equals(name, "period_grain", StringComparison.OrdinalIgnoreCase));
            if (grainIndex >= 0)
            {
                dashboardFilters.Insert(grainIndex + 1, filterName);
                return;
            }
        }

        dashboardFilters.Add(filterName);
    }
}
