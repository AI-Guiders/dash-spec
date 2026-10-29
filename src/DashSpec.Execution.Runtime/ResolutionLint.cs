
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>ADR-0057 P2 — warn when multiple chain sources compete on one slot.</summary>
public static class ResolutionLint
{
    public const string CodeDuplicateStrongSource = "DS0571";

    private static readonly string[] LegacyChartChromeKeys =
    [
        "legend", "height", "visible_rows", "stacked", "orientation", "fill",
        "scale_value", "scale_measure", "scale_x", "scale_y", "value_scale", "y_format",
        "y_max", "value_axis_max", "color_mode", "default", "colors",
    ];

    public static IReadOnlyList<ResolutionLintFinding> Analyze(
        DashboardDocument document,
        CatalogEntryDefinition? catalogEntry = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var findings = new List<ResolutionLintFinding>();
        if (catalogEntry is not null)
        {
            findings.AddRange(AnalyzeCatalogProd(document, catalogEntry));
            findings.AddRange(AnalyzeDashboardEmbed(document, catalogEntry));
        }

        findings.AddRange(AnalyzeComposition(document));
        findings.AddRange(AnalyzePlacement(document));
        findings.AddRange(AnalyzePresetResolution(document));
        findings.AddRange(AnalyzeFilterUsage(document));
        return findings;
    }

    /// <summary>
    /// DS0572: a diagram module's chrome <c>use &lt;preset&gt;</c> must resolve in the
    /// document's library — otherwise height/colors silently fall to defaults.
    /// </summary>
    private static IEnumerable<ResolutionLintFinding> AnalyzePresetResolution(
        DashboardDocument document)
    {
        var availablePresets = document.ResolvedChartChromePresets;

        foreach (var moduleDiagram in document.ResolvedModuleDiagrams.Values)
        {
            var presetName = moduleDiagram.Presentation?.Value?.UsePreset?.Value;
            if (!string.IsNullOrWhiteSpace(presetName) &&
                !availablePresets.ContainsKey(presetName))
            {
                yield return Finding(
                    "diagram.preset_not_found",
                    null,
                    $"diagram.use.{presetName}",
                    "library.chart_chrome_presets",
                    $"diagram module '{moduleDiagram.Diagram.Kind}' uses preset '{presetName}' " +
                    "which is not in the library — height/colors fall to defaults. " +
                    "Import the preset namespace (import presentations from <ns>) " +
                    "or declare the preset inline.");
            }
        }

        // stretch_mode = fill with default height: the fill floor silently becomes
        // the presentation default (280/320) instead of the authored preset value.
        foreach (var card in document.Cards)
        {
            var props = card.Diagram.Properties;
            if (props.TryGetValue("stretch_mode", out var stretch) &&
                string.Equals(stretch, "fill", StringComparison.OrdinalIgnoreCase))
            {
                var presetName = card.Presentation?.Value?.UsePreset?.Value ??
                    moduleDiagramPresetFor(card, document);
                if (!string.IsNullOrWhiteSpace(presetName) &&
                    !availablePresets.ContainsKey(presetName))
                {
                    yield return Finding(
                        "card.fill_height_default",
                        null,
                        $"card.{card.Id}.stretch_mode",
                        "library.chart_chrome_presets",
                        $"card '{card.Id}': stretch_mode = fill but preset '{presetName}' not resolved — " +
                        "fill floor falls to the default height (280/320). " +
                        "This is almost certainly not what you want.");
                }
            }
        }
    }

    private static string? moduleDiagramPresetFor(CardDefinition card, DashboardDocument document)
    {
        // Cards reference module diagrams by name; find the matching module's preset
        foreach (var (id, moduleDiagram) in document.ResolvedModuleDiagrams)
        {
            if (string.Equals(moduleDiagram.Diagram.Kind, card.Diagram.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return moduleDiagram.Presentation?.Value?.UsePreset?.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// DS0573: a report-level filter declared in <c>filters</c> that no card binds
    /// is dead weight — the widget renders but filters nothing.
    /// </summary>
    private static IEnumerable<ResolutionLintFinding> AnalyzeFilterUsage(
        DashboardDocument document)
    {
        var usedFilterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in document.Cards)
        {
            foreach (var name in card.BoundFilters)
            {
                usedFilterNames.Add(name);
            }

            foreach (var name in card.LocalFilters)
            {
                usedFilterNames.Add(name);
            }
        }

        foreach (var tab in document.Tabs)
        {
            foreach (var cardId in tab.CardIds)
            {
                var card = document.Cards.FirstOrDefault(c =>
                    string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
                if (card is null) continue;
                foreach (var name in card.BoundFilters)
                {
                    usedFilterNames.Add(name);
                }
            }
        }

        foreach (var filter in document.Filters)
        {
            if (!usedFilterNames.Contains(filter.Name))
            {
                yield return Finding(
                    "filter.unused",
                    null,
                    $"filter.{filter.Name}",
                    "report.filters",
                    $"filter '{filter.Name}' is declared but not bound to any card — " +
                    "it renders a widget that filters nothing.",
                    informational: true);
            }
        }
    }

    private static IEnumerable<ResolutionLintFinding> AnalyzeCatalogProd(
        DashboardDocument document,
        CatalogEntryDefinition entry)
    {
        if (HasText(entry.Title) && HasText(document.Title))
        {
            yield return Finding(
                "report.header_title",
                ResolutionMode.CatalogProd,
                "catalog.entry.title",
                "report.title",
                "report.title wins over catalog entry title in catalog.prod.");
        }

        foreach (var tab in document.Tabs)
        {
            if (HasText(tab.Label?.Value) && HasText(entry.Title))
            {
                yield return Finding(
                    "tab.label",
                    ResolutionMode.CatalogProd,
                    "tab.title",
                    "catalog.entry.title",
                    $"tab '{tab.Id}' title is ignored in catalog.prod; catalog entry title is used.");
            }
        }

    }

    private static IEnumerable<ResolutionLintFinding> AnalyzeDashboardEmbed(
        DashboardDocument document,
        CatalogEntryDefinition entry)
    {
        if (HasText(document.Title) && HasText(entry.Title))
        {
            yield return Finding(
                "report.header_title",
                ResolutionMode.DashboardEmbed,
                "report.title",
                "catalog.entry.title",
                "catalog entry title wins over report.title in dashboard.embed.");
        }

        foreach (var tab in document.Tabs)
        {
            if (HasText(tab.Label?.Value) && HasText(entry.Title))
            {
                yield return Finding(
                    "tab.label",
                    ResolutionMode.DashboardEmbed,
                    "tab.title",
                    "catalog.entry.title",
                    $"catalog entry title wins over tab '{tab.Id}' title in dashboard.embed.");
            }
        }

        foreach (var card in document.Cards)
        {
            if (HasText(card.Title) && HasText(entry.Title))
            {
                yield return Finding(
                    "card.chrome_title",
                    ResolutionMode.DashboardEmbed,
                    "catalog.entry.title",
                    "card.title",
                    $"card '{card.Id}' title wins over catalog entry title in dashboard.embed.");
            }
        }
    }

    private static IEnumerable<ResolutionLintFinding> AnalyzeComposition(DashboardDocument document)
    {
        foreach (var card in document.Cards)
        {
            if (card.Presentation is not null)
            {
                foreach (var key in LegacyChartChromeKeys)
                {
                    if (card.Diagram.Properties.ContainsKey(key))
                    {
                        yield return Finding(
                            "diagram.chart_chrome",
                            null,
                            $"diagram.{key}",
                            "card.presentation",
                            $"card '{card.Id}': inline diagram '{key}' is shadowed by card presentation block.");
                    }
                }
            }

            if (card.SeriesTransform is not null &&
                card.Diagram.Properties.ContainsKey("max_series"))
            {
                yield return Finding(
                    "diagram.series_transform",
                    null,
                    "diagram.max_series",
                    "card.series_transform",
                    $"card '{card.Id}': diagram max_series is shadowed by card series_transform block.");
            }
        }
    }

    private static IEnumerable<ResolutionLintFinding> AnalyzePlacement(DashboardDocument document)
    {
        var boardCardIds = CollectLayoutBoardCardIds(document);
        foreach (var card in document.Cards)
        {
            if (card.Placement is null || !boardCardIds.Contains(card.Id))
            {
                continue;
            }

            yield return Finding(
                "tab.card_placement",
                null,
                "layout.board",
                "card.place",
                $"card '{card.Id}' explicit place {{ }} overrides layout board placement (intentional override).",
                informational: true);
        }
    }

    private static HashSet<string> CollectLayoutBoardCardIds(DashboardDocument document)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cards = document.Cards;

        foreach (var tab in document.Tabs)
        {
            CollectBoard(tab.LayoutBoard?.Value, tab.CardIds, cards, $"tab '{tab.Id}'", ids);
        }

        if (document.Pages?.Value is { } pages)
        {
            foreach (var page in pages)
            {
                var tabCards = ResolveTabCards(document, page.TabId?.Value);
                CollectBoard(page.LayoutBoard?.Value, tabCards.Select(c => c.Id).ToList(), tabCards, $"page '{page.Id}'", ids);
            }
        }

        return ids;
    }

    private static void CollectBoard(
        LayoutBoardDefinition? board,
        IReadOnlyList<string> tabCardIds,
        IReadOnlyList<CardDefinition> tabCards,
        string context,
        ISet<string> ids)
    {
        if (board is null)
        {
            return;
        }

        var nests = CollectNestDefinitions(board, context);
        foreach (var entry in board.Entries)
        {
            switch (entry)
            {
                case LayoutBoardEntry.CardRow cardRow:
                    foreach (var token in cardRow.Item)
                    {
                        var target = LayoutBoardRefResolver.Resolve(
                            LayoutBoardCellParser.refToken(token),
                            tabCards,
                            nests,
                            context);
                        if (target.Kind == LayoutBoardRefKind.Card)
                        {
                            ids.Add(target.Id);
                        }
                    }

                    break;
                case LayoutBoardEntry.GroupRow groupRow:
                    var group = groupRow.Item;
                    foreach (var row in group.Rows)
                    {
                        foreach (var token in row)
                        {
                            ids.Add(CardLayoutRefBridge.Resolve(token, tabCards, context));
                        }
                    }

                    break;
                case LayoutBoardEntry.NestRow nestRow:
                    var nest = nestRow.Item;
                    foreach (var row in nest.Rows)
                    {
                        foreach (var token in row)
                        {
                            ids.Add(CardLayoutRefBridge.Resolve(token, tabCards, context));
                        }
                    }

                    break;
            }
        }
    }

    private static Dictionary<string, LayoutBoardNestDefinition> CollectNestDefinitions(
        LayoutBoardDefinition board,
        string context)
    {
        var nests = new Dictionary<string, LayoutBoardNestDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in board.Entries)
        {
            if (entry is not LayoutBoardEntry.NestRow nestRow)
            {
                continue;
            }

            var nest = nestRow.Item;

            if (!nests.TryAdd(nest.Id, nest))
            {
                throw new DashSpecParseException($"{context}: duplicate layout nest id '{nest.Id}'.");
            }
        }

        return nests;
    }

    private static IReadOnlyList<CardDefinition> ResolveTabCards(DashboardDocument document, string? tabId)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            return document.Cards;
        }

        var tab = document.Tabs.FirstOrDefault(t =>
            string.Equals(t.Id, tabId, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            return document.Cards;
        }

        return tab.CardIds
            .Select(token => CardLayoutRefBridge.Resolve(token, document.Cards, $"tab '{tab.Id}'"))
            .Select(id => document.Cards.Single(c =>
                string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static ResolutionLintFinding Finding(
        string slot,
        ResolutionMode? mode,
        string loser,
        string winner,
        string message,
        bool informational = false) =>
        new(
            CodeDuplicateStrongSource,
            slot,
            mode,
            loser,
            winner,
            informational ? ResolutionLintSeverity.Information : ResolutionLintSeverity.Warning,
            message);

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

public enum ResolutionLintSeverity
{
    Warning,
    Information,
}

public sealed record ResolutionLintFinding(
    string Code,
    string Slot,
    ResolutionMode? Mode,
    string LoserSource,
    string WinnerSource,
    ResolutionLintSeverity Severity,
    string Message)
{
    public string FormatMessage()
    {
        var modeSuffix = Mode is null ? string.Empty : $" [{Mode}]";
        return $"{Code}{modeSuffix} {Slot}: {Message} (winner: {WinnerSource}, ignored: {LoserSource}).";
    }
}
