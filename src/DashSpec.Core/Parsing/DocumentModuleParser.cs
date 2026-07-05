using DashSpec.Core.Analysis;
using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal enum ReportBodyMode
{
    DashboardRoot,
    TabStandalone,
    TabEmbedded,
}

internal static class DocumentModuleParser
{
    public static bool IsBlockModuleFormat(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var reader = ParserUtilities.CreateReader(text);
        reader.SkipFileDirectives();
        reader.SkipNewlines();
        if (!reader.IsAt(TokenKind.At))
        {
            return false;
        }

        reader.Advance();
        if (!reader.TryKeyword("dashboard") && !reader.TryKeyword("tab"))
        {
            return false;
        }

        _ = reader.ReadIdent();
        reader.SkipNewlines();
        return reader.IsAt(TokenKind.LBrace);
    }

    public static DashboardDocument ParseDocument(string text, string? specDirectory = null) =>
        ParseDocument(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument ParseDocument(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipNewlines();
        reader.Expect(TokenKind.At);

        if (reader.TryKeyword("tab"))
        {
            return ComposeTabStandalone(reader, specDirectory, parseOptions);
        }

        if (reader.TryKeyword("dashboard"))
        {
            var document = ParseDashboard(reader, specDirectory, parseOptions);
            if (document.Tabs.All(t => string.IsNullOrWhiteSpace(t.DashspecPath)))
            {
                DashboardValidator.Validate(document);
                return document;
            }

            if (string.IsNullOrWhiteSpace(specDirectory))
            {
                throw new DashSpecParseException(
                    "Tab dashspec references require specDirectory when parsing.");
            }

            return DashboardComposer.MergeTabModules(document, specDirectory, parseOptions);
        }

        throw new DashSpecParseException("Block module must start with @dashboard or @tab.");
    }

    public static TabModuleContent ParseTabEmbedded(
        string text,
        string expectedTabId,
        string? specDirectory,
        IReadOnlyList<FilterDefinition>? parentFilters,
        DashSpecParseOptions? parseOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTabId);
        parseOptions ??= DashSpecParseOptions.Default;

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipNewlines();
        reader.Expect(TokenKind.At);
        reader.ExpectKeyword("tab");
        var tabId = reader.ReadIdent();
        if (!string.Equals(tabId, expectedTabId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Tab dashspec for '{expectedTabId}' must declare @tab '{expectedTabId}', found '{tabId}'.");
        }

        var result = ParseTabModuleShell(
            reader,
            tabId,
            DashboardShellMode.TabModuleEmbedded,
            specDirectory,
            parentFilters,
            ReportBodyMode.TabEmbedded,
            parseOptions);

        if (result.Shell.Cards.Count == 0)
        {
            throw new DashSpecParseException($"Tab module '{tabId}' must declare at least one card.");
        }

        return new TabModuleContent(
            tabId,
            result.Shell.TabModuleLabel,
            result.Shell.ExportedTabLocalFilters,
            result.Shell.Cards,
            result.Shell.LayoutBoard,
            result.Shell.Includes.ExportDefinitions());
    }

    public static string? ReadRuntimeManifest(string text)
    {
        if (!IsBlockModuleFormat(text))
        {
            return null;
        }

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipNewlines();
        reader.Expect(TokenKind.At);
        _ = reader.TryKeyword("dashboard") || reader.TryKeyword("tab");
        _ = reader.ReadIdent();
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.TryKeyword("runtime"))
            {
                var props = PropertyBlockParser.Parse(reader, PropertySchemas.Runtime, "runtime");
                return props.GetValueOrDefault("manifest");
            }

            if (reader.TryKeyword("configuration") ||
                reader.TryKeyword("wiring") ||
                reader.TryKeyword("report"))
            {
                return null;
            }

            if (reader.TryModuleInclude(out _))
            {
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        return null;
    }

    public static string? ReadConfigurationValue(string text, string key)
    {
        if (!IsBlockModuleFormat(text))
        {
            return null;
        }

        var reader = ParserUtilities.CreateReader(text);
        reader.SkipNewlines();
        reader.Expect(TokenKind.At);
        _ = reader.TryKeyword("dashboard") || reader.TryKeyword("tab");
        _ = reader.ReadIdent();
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.TryKeyword("configuration"))
            {
                var props = PropertyBlockParser.Parse(reader, PropertySchemas.Configuration, "configuration");
                return props.GetValueOrDefault(key);
            }

            if (reader.TryKeyword("runtime") || reader.TryKeyword("wiring"))
            {
                SkipBlock(reader);
                continue;
            }

            if (reader.TryKeyword("report"))
            {
                return null;
            }

            if (reader.TryModuleInclude(out _))
            {
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        return null;
    }

    private sealed record ModuleShellResult(
        DashboardShellContext Shell,
        SqlDialect SqlDialect,
        string? PalettePath,
        string? DiagramLibraryPath,
        string? ReportTitle);

    private static DashboardDocument ComposeTabStandalone(
        TokenReader reader,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var tabId = reader.ReadIdent();
        var result = ParseTabModuleShell(
            reader,
            tabId,
            DashboardShellMode.TabModuleStandalone,
            specDirectory,
            parentFilters: null,
            ReportBodyMode.TabStandalone,
            parseOptions);

        if (result.Shell.Cards.Count == 0)
        {
            throw new DashSpecParseException($"Standalone @tab '{tabId}' must declare at least one card.");
        }

        var title = result.ReportTitle ?? result.Shell.TabModuleLabel ?? tabId;
        var tabs = new List<TabDefinition>
        {
            new(tabId, title, result.Shell.Cards.Select(c => c.Id).ToList(), LayoutBoard: result.Shell.LayoutBoard),
        };

        var cards = TabParser.AssignTabs(result.Shell.Cards, tabs);
        var dashboardFilters = ToolbarPlacementResolver.ResolveFilterNames(
            result.Shell.Filters,
            result.Shell.DashboardFilters,
            result.Shell.ToolbarBoard);

        var document = new DashboardDocument(
            tabId,
            title,
            result.Shell.ConnectorId,
            result.SqlDialect,
            result.DiagramLibraryPath,
            result.PalettePath,
            result.Shell.ColorPalette,
            result.Shell.Layout,
            result.Shell.FiltersChrome,
            result.Shell.Filters,
            dashboardFilters,
            tabs,
            cards,
            result.Shell.ToolbarBoard,
            result.Shell.ModuleExtensions,
            result.Shell.Includes.ExportDefinitions());

        DashboardValidator.Validate(document);
        return document;
    }

    private static DashboardDocument ParseDashboard(
        TokenReader reader,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var dashboardId = reader.ReadIdent();
        var (shell, sqlDialect, palettePath, diagramLibraryPath, reportTitle) =
            ParseDashboardShell(reader, specDirectory, parseOptions);

        if (string.IsNullOrWhiteSpace(reportTitle))
        {
            throw new DashSpecParseException($"@dashboard '{dashboardId}' report requires a title string.");
        }

        var cards = TabParser.AssignTabs(shell.Cards, shell.Tabs);
        var dashboardFilters = ToolbarPlacementResolver.ResolveFilterNames(
            shell.Filters,
            shell.DashboardFilters,
            shell.ToolbarBoard);

        return new DashboardDocument(
            dashboardId,
            reportTitle,
            shell.ConnectorId,
            sqlDialect,
            diagramLibraryPath,
            palettePath,
            shell.ColorPalette,
            shell.Layout,
            shell.FiltersChrome,
            shell.Filters,
            dashboardFilters,
            shell.Tabs,
            cards,
            shell.ToolbarBoard,
            shell.ModuleExtensions,
            shell.Includes.ExportDefinitions());
    }

    private static (DashboardShellContext Shell, SqlDialect SqlDialect, string? PalettePath, string? DiagramLibraryPath, string? ReportTitle)
        ParseDashboardShell(TokenReader reader, string? specDirectory, DashSpecParseOptions parseOptions)
    {
        var includes = new ModuleIncludeState();
        var moduleExtensions = ModuleExtensionsDefinition.Empty;
        var sqlDialect = SqlDialect.TSql;
        string? palettePath = null;
        string? diagramLibraryPath = null;
        string? connectorId = null;
        string? paletteUse = null;
        var layout = LayoutDefinition.Default;
        LayoutBoardDefinition? wiringLayoutBoard = null;
        LayoutBoardDefinition? wiringToolbarBoard = null;

        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        DashboardShellContext? shell = null;
        string? reportTitle = null;

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (TryParseEnvelopeSection(
                    reader,
                    DocumentModuleKind.Dashboard,
                    specDirectory,
                    includes,
                    ref sqlDialect,
                    ref palettePath,
                    ref diagramLibraryPath,
                    ref connectorId,
                    ref paletteUse,
                    ref layout,
                    ref moduleExtensions,
                    out var sectionLayoutBoard,
                    out var sectionToolbarBoard))
            {
                if (sectionLayoutBoard is not null)
                {
                    wiringLayoutBoard = sectionLayoutBoard;
                }

                if (sectionToolbarBoard is not null)
                {
                    wiringToolbarBoard = sectionToolbarBoard;
                }

                continue;
            }

            if (reader.TryKeyword("report"))
            {
                shell = CreateShell(
                    DashboardShellMode.DashboardBody,
                    specDirectory,
                    tabModuleId: null,
                    parentFilters: null,
                    includes,
                    connectorId,
                    paletteUse,
                    layout,
                    wiringLayoutBoard,
                    wiringToolbarBoard,
                    parseOptions,
                    moduleExtensions);

                reportTitle = ReadOptionalReportTitle(reader);
                ParseReportBlock(reader, shell, ReportBodyMode.DashboardRoot, out _);
                continue;
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);
        if (shell is null)
        {
            throw new DashSpecParseException("@dashboard module body is empty.");
        }

        return (shell, sqlDialect, palettePath, diagramLibraryPath, reportTitle);
    }

    private static ModuleShellResult ParseTabModuleShell(
        TokenReader reader,
        string tabId,
        DashboardShellMode mode,
        string? specDirectory,
        IReadOnlyList<FilterDefinition>? parentFilters,
        ReportBodyMode reportMode,
        DashSpecParseOptions parseOptions)
    {
        var includes = new ModuleIncludeState();
        var moduleExtensions = ModuleExtensionsDefinition.Empty;
        var sqlDialect = SqlDialect.TSql;
        string? palettePath = null;
        string? diagramLibraryPath = null;
        string? connectorId = null;
        string? paletteUse = null;
        var layout = LayoutDefinition.Default;
        LayoutBoardDefinition? wiringLayoutBoard = null;
        LayoutBoardDefinition? wiringToolbarBoard = null;

        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        DashboardShellContext? shell = null;
        string? reportTitle = null;

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (TryParseEnvelopeSection(
                    reader,
                    DocumentModuleKind.Tab,
                    specDirectory,
                    includes,
                    ref sqlDialect,
                    ref palettePath,
                    ref diagramLibraryPath,
                    ref connectorId,
                    ref paletteUse,
                    ref layout,
                    ref moduleExtensions,
                    out var sectionLayoutBoard,
                    out var sectionToolbarBoard))
            {
                if (sectionLayoutBoard is not null)
                {
                    wiringLayoutBoard = sectionLayoutBoard;
                }

                if (sectionToolbarBoard is not null)
                {
                    wiringToolbarBoard = sectionToolbarBoard;
                }

                continue;
            }

            if (reader.TryKeyword("report"))
            {
                shell = CreateShell(
                    mode,
                    specDirectory,
                    tabId,
                    parentFilters,
                    includes,
                    connectorId,
                    paletteUse,
                    layout,
                    wiringLayoutBoard,
                    wiringToolbarBoard,
                    parseOptions,
                    moduleExtensions);

                reportTitle = ReadOptionalReportTitle(reader);
                ParseReportBlock(reader, shell, reportMode, out var moduleLabel);
                shell.TabModuleLabel ??= moduleLabel;
                continue;
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);
        if (shell is null)
        {
            throw new DashSpecParseException($"@tab '{tabId}' module body is empty.");
        }

        return new ModuleShellResult(shell, sqlDialect, palettePath, diagramLibraryPath, reportTitle);
    }

    private static bool TryParseEnvelopeSection(
        TokenReader reader,
        DocumentModuleKind moduleKind,
        string? specDirectory,
        ModuleIncludeState includes,
        ref SqlDialect sqlDialect,
        ref string? palettePath,
        ref string? diagramLibraryPath,
        ref string? connectorId,
        ref string? paletteUse,
        ref LayoutDefinition layout,
        ref ModuleExtensionsDefinition moduleExtensions,
        out LayoutBoardDefinition? layoutBoard,
        out LayoutBoardDefinition? toolbarBoard)
    {
        layoutBoard = null;
        toolbarBoard = null;

        if (reader.TryKeyword("runtime"))
        {
            var props = PropertyBlockParser.Parse(reader, PropertySchemas.Runtime, "runtime");
            _ = props.TryGetValue("manifest", out _);
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("configuration"))
        {
            var props = PropertyBlockParser.Parse(reader, PropertySchemas.Configuration, "configuration");
            if (props.TryGetValue("sqldialect", out var dialectRaw))
            {
                sqlDialect = SqlDialectParser.Parse(dialectRaw);
            }

            props.TryGetValue("palette", out palettePath);
            props.TryGetValue("diagramlibrary", out diagramLibraryPath);
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryModuleInclude(out var includeReference))
        {
            if (string.IsNullOrWhiteSpace(specDirectory))
            {
                throw new DashSpecParseException("!include requires specDirectory when parsing.");
            }

            IncludeExpander.Expand(includeReference, specDirectory, moduleKind, includes);
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("extensions"))
        {
            moduleExtensions = ModuleExtensionsParser.Parse(reader);
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("wiring"))
        {
            ParseWiringBlock(
                reader,
                out var wiredConnector,
                out var wiredPalette,
                ref layout,
                out layoutBoard,
                out toolbarBoard);
            if (wiredConnector is not null)
            {
                connectorId = wiredConnector;
            }

            if (wiredPalette is not null)
            {
                paletteUse = wiredPalette;
            }

            reader.SkipNewlines();
            return true;
        }

        return false;
    }

    private static void ParseWiringBlock(
        TokenReader reader,
        out string? connectorId,
        out string? paletteUse,
        ref LayoutDefinition layout,
        out LayoutBoardDefinition? layoutBoard,
        out LayoutBoardDefinition? toolbarBoard)
    {
        connectorId = null;
        paletteUse = null;
        layoutBoard = null;
        toolbarBoard = null;

        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("use"))
            {
                var useKind = reader.ReadIdent();
                var useId = reader.ReadIdent();
                if (string.Equals(useKind, "connector", StringComparison.OrdinalIgnoreCase))
                {
                    connectorId = useId;
                }
                else if (string.Equals(useKind, "palette", StringComparison.OrdinalIgnoreCase))
                {
                    paletteUse = useId;
                }
                else
                {
                    throw new DashSpecParseException($"wiring use must be connector or palette, got '{useKind}'.");
                }

                continue;
            }

            if (reader.TryKeyword("layout"))
            {
                if (reader.TryPeekIdent(out var layoutKind) &&
                    string.Equals(layoutKind, "grid", StringComparison.OrdinalIgnoreCase))
                {
                    layout = LayoutParser.ParseGrid(reader);
                    continue;
                }

                if (reader.TryPeekIdent(out layoutKind) &&
                    string.Equals(layoutKind, "board", StringComparison.OrdinalIgnoreCase))
                {
                    _ = reader.ReadIdent();
                    layoutBoard = LayoutParser.ParseBoard(reader);
                    continue;
                }

                throw reader.Unexpected("layout grid or layout board");
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);
    }

    private static void ParseReportBlock(
        TokenReader reader,
        DashboardShellContext shell,
        ReportBodyMode mode,
        out string? moduleLabel)
    {
        moduleLabel = null;
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("standalone"))
            {
                if (mode is ReportBodyMode.TabEmbedded)
                {
                    SkipBlock(reader);
                    continue;
                }

                ParseStandaloneBlock(reader, shell);
                continue;
            }

            if (reader.TryKeyword("filters"))
            {
                if (reader.TryPeekIdent(out var filtersNext) &&
                    (string.Equals(filtersNext, "dashboard", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(filtersNext, "chrome", StringComparison.OrdinalIgnoreCase)))
                {
                    DashboardShellParser.ParseFiltersChromePublic(reader, shell, assign: true);
                    reader.SkipNewlines();
                    continue;
                }

                ParseFiltersBlock(reader, shell, mode);
                continue;
            }

            if (reader.TryKeyword("toolbar"))
            {
                DashboardShellParser.ParseFiltersChromePublic(reader, shell, assign: true);
                reader.SkipNewlines();
                continue;
            }

            if (DashboardShellParser.TryParseStatement(reader, shell))
            {
                continue;
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);
    }

    private static void ParseStandaloneBlock(TokenReader reader, DashboardShellContext shell)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (reader.TryKeyword("filter"))
            {
                shell.Filters.Add(FilterParser.Parse(reader));
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("toolbar") || reader.TryKeyword("filters"))
            {
                DashboardShellParser.ParseFiltersChromePublic(reader, shell, assign: true);
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }

        reader.Expect(TokenKind.RBrace);
    }

    private static void ParseFiltersBlock(TokenReader reader, DashboardShellContext shell, ReportBodyMode mode)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            if (!reader.TryKeyword("filter"))
            {
                throw reader.Unexpected();
            }

            var filter = FilterParser.Parse(reader);
            if (mode is ReportBodyMode.TabEmbedded)
            {
                shell.TabLocalFilters.Add(filter);
            }
            else
            {
                shell.Filters.Add(filter);
            }

            reader.SkipNewlines();
        }

        reader.Expect(TokenKind.RBrace);
    }

    private static void SkipBlock(TokenReader reader)
    {
        reader.Expect(TokenKind.LBrace);
        var depth = 1;
        while (depth > 0 && !reader.IsEof)
        {
            if (reader.IsAt(TokenKind.LBrace))
            {
                reader.Advance();
                depth++;
                continue;
            }

            if (reader.IsAt(TokenKind.RBrace))
            {
                reader.Advance();
                depth--;
                continue;
            }

            reader.Advance();
        }
    }

    private static DashboardShellContext CreateShell(
        DashboardShellMode mode,
        string? specDirectory,
        string? tabModuleId,
        IReadOnlyList<FilterDefinition>? parentFilters,
        ModuleIncludeState includes,
        string? connectorId,
        string? paletteUse,
        LayoutDefinition layout,
        LayoutBoardDefinition? layoutBoard,
        LayoutBoardDefinition? toolbarBoard,
        DashSpecParseOptions parseOptions,
        ModuleExtensionsDefinition moduleExtensions)
    {
        if (layoutBoard is not null && includes.LayoutBoard is not null)
        {
            throw new DashSpecParseException("Tab module declares more than one card layout board.");
        }

        if (toolbarBoard is not null && includes.ToolbarBoard is not null)
        {
            throw new DashSpecParseException("Dashboard module declares more than one toolbar layout board.");
        }

        return new()
        {
            Mode = mode,
            SpecDirectory = specDirectory,
            TabModuleId = tabModuleId,
            ParentFilters = parentFilters,
            Includes = includes,
            ConnectorId = connectorId,
            ColorPalette = paletteUse,
            Layout = layout,
            LayoutBoard = layoutBoard ?? includes.LayoutBoard,
            ToolbarBoard = toolbarBoard ?? includes.ToolbarBoard,
            ParseOptions = RestrictExtensionBlocksForModule(parseOptions, moduleExtensions),
            ModuleExtensions = moduleExtensions,
        };
    }

    private static DashSpecParseOptions RestrictExtensionBlocksForModule(
        DashSpecParseOptions options,
        ModuleExtensionsDefinition moduleExtensions)
    {
        if (moduleExtensions.EnabledPluginIds.Count == 0)
        {
            return options;
        }

        var allowed = moduleExtensions.EnabledPluginIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keywords = options.ExtensionBlockKeywords
            .Where(keyword =>
                options.ExtensionBlockPluginIds.TryGetValue(keyword, out var pluginId) &&
                allowed.Contains(pluginId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new DashSpecParseOptions
        {
            ExtensionBlockKeywords = keywords,
            ExtensionBlockPluginIds = options.ExtensionBlockPluginIds,
            PhraseTemplates = options.PhraseTemplates,
            KnownActionHandlers = options.KnownActionHandlers,
            KnownInteractionHandlers = options.KnownInteractionHandlers,
        };
    }

    private static string? ReadOptionalReportTitle(TokenReader reader)
    {
        reader.SkipNewlines();
        return reader.CurrentKind is TokenKind.String ? reader.ReadString() : null;
    }
}
