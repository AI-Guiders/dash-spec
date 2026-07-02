using DashSpec.Core.Analysis;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal sealed record TabModuleContent(
    string TabId,
    string? Label,
    IReadOnlyList<FilterDefinition> Filters,
    IReadOnlyList<CardDefinition> Cards,
    LayoutBoardDefinition? LayoutBoard = null);

internal static class TabModuleParser
{
    public static TabModuleContent ParseEmbedded(
        string text,
        string expectedTabId,
        string? specDirectory = null,
        IReadOnlyList<FilterDefinition>? parentFilters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTabId);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();

        var tabId = ReadTabDirective(reader);
        if (!string.Equals(tabId, expectedTabId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Tab dashspec for '{expectedTabId}' must declare @tab '{expectedTabId}', found '{tabId}'.");
        }

        var shell = new DashboardShellContext
        {
            Mode = DashboardShellMode.TabModuleEmbedded,
            SpecDirectory = specDirectory,
            TabModuleId = tabId,
            ParentFilters = parentFilters,
        };

        reader.SkipNewlines();
        while (!reader.IsEof)
        {
            if (!DashboardShellParser.TryParseStatement(reader, shell))
            {
                if (reader.IsEof)
                {
                    break;
                }

                throw reader.Unexpected();
            }
        }

        if (shell.Cards.Count == 0)
        {
            throw new DashSpecParseException($"Tab module '{tabId}' must declare at least one card.");
        }

        return new TabModuleContent(
            tabId,
            shell.TabModuleLabel,
            shell.ExportedTabLocalFilters,
            shell.Cards,
            shell.LayoutBoard);
    }

    public static DashboardDocument ComposeStandalone(string text, string? specDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        var sqlDialect = reader.ConsumedSqlDialect;
        var diagramLibraryPath = reader.ConsumedDiagramLibraryPath;
        var palettePath = reader.ConsumedPalettePath;

        var tabId = ReadTabDirective(reader);
        var shell = new DashboardShellContext
        {
            Mode = DashboardShellMode.TabModuleStandalone,
            SpecDirectory = specDirectory,
            TabModuleId = tabId,
        };

        reader.SkipNewlines();
        while (!reader.IsEof)
        {
            if (!DashboardShellParser.TryParseStatement(reader, shell))
            {
                throw reader.Unexpected();
            }
        }

        if (shell.Cards.Count == 0)
        {
            throw new DashSpecParseException($"Standalone @tab '{tabId}' must declare at least one card.");
        }

        var title = shell.TabModuleLabel ?? tabId;
        var tabs = new List<TabDefinition>
        {
            new(tabId, title, shell.Cards.Select(c => c.Id).ToList(), LayoutBoard: shell.LayoutBoard),
        };

        var cards = TabParser.AssignTabs(shell.Cards, tabs);
        var dashboardFilters = ToolbarPlacementResolver.ResolveFilterNames(
            shell.Filters,
            shell.DashboardFilters,
            shell.ToolbarBoard);
        var document = new DashboardDocument(
            tabId,
            title,
            shell.ConnectorId,
            sqlDialect,
            diagramLibraryPath,
            palettePath,
            shell.ColorPalette,
            shell.Layout,
            shell.FiltersChrome,
            shell.Filters,
            dashboardFilters,
            tabs,
            cards,
            shell.ToolbarBoard);

        DashboardValidator.Validate(document);
        return document;
    }

    private static string ReadTabDirective(TokenReader reader)
    {
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("tab");
        return reader.ReadIdent();
    }
}
