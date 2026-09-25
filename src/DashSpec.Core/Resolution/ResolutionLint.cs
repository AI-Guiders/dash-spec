using DashSpec.Core.Layout;
using DashSpec.Core.Model;

namespace DashSpec.Core.Resolution;

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
        return findings;
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
            if (HasText(tab.Label) && HasText(entry.Title))
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
            if (HasText(tab.Label) && HasText(entry.Title))
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
            CollectBoard(tab.LayoutBoard, tab.CardIds, cards, $"tab '{tab.Id}'", ids);
        }

        if (document.Pages is not null)
        {
            foreach (var page in document.Pages)
            {
                var tabCards = ResolveTabCards(document, page.TabId);
                CollectBoard(page.LayoutBoard, tabCards.Select(c => c.Id).ToList(), tabCards, $"page '{page.Id}'", ids);
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

        foreach (var entry in board.Entries)
        {
            switch (entry)
            {
                case LayoutBoardCardRow cardRow:
                    foreach (var token in cardRow.CardIds)
                    {
                        ids.Add(CardLayoutRefResolver.Resolve(token, tabCards, context));
                    }

                    break;
                case LayoutBoardGroupRow { Group: var group }:
                    foreach (var row in group.Rows)
                    {
                        foreach (var token in row)
                        {
                            ids.Add(CardLayoutRefResolver.Resolve(token, tabCards, context));
                        }
                    }

                    break;
            }
        }
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
            .Select(token => CardLayoutRefResolver.Resolve(token, document.Cards, $"tab '{tab.Id}'"))
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
