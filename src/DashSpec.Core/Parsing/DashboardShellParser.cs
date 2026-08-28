using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal enum DashboardShellMode
{
    DashboardBody,
    TabModuleStandalone,
    TabModuleEmbedded,
}

internal sealed class DashboardShellContext
{
    public required DashboardShellMode Mode { get; init; }

    public string? SpecDirectory { get; init; }

    public string? TabModuleId { get; init; }

    public IReadOnlyList<FilterDefinition>? ParentFilters { get; init; }

    public List<FilterDefinition> Filters { get; } = [];

    public List<FilterDefinition> ShellFilters { get; } = [];

    public List<FilterDefinition> TabLocalFilters { get; } = [];

    public List<string> DashboardFilters { get; } = [];

    public List<TabDefinition> Tabs { get; } = [];

    public List<CardDefinition> Cards { get; } = [];

    public string? ConnectorId { get; set; }

    public string? ColorPalette { get; set; }

    public LayoutDefinition Layout { get; set; } = LayoutDefinition.Default;

    public FiltersChromeDefinition FiltersChrome { get; set; } = FiltersChromeDefinition.Default;

    public LayoutBoardDefinition? LayoutBoard { get; set; }

    public LayoutBoardDefinition? ToolbarBoard { get; set; }

    public string? TabModuleLabel { get; set; }

    public ModuleIncludeState Includes { get; init; } = new();

    public DashSpecParseOptions ParseOptions { get; init; } = DashSpecParseOptions.Default;

    public string? CurrentPhaseId { get; set; }

    public string? CurrentPageId { get; set; }

    public List<ReportPageDefinition> Pages { get; } = [];

    public ModuleExtensionsDefinition ModuleExtensions { get; set; } = ModuleExtensionsDefinition.Empty;

    public Dictionary<string, string> CommandAliases { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<FilterDefinition> CardBindValidationFilters =>
        MergeFilterScopes(ParentFilters, ShellFilters, TabLocalFilters, Filters);

    public IReadOnlyList<FilterDefinition> ExportedTabLocalFilters =>
        Mode is DashboardShellMode.TabModuleEmbedded ? TabLocalFilters : [];

    public static IReadOnlyList<FilterDefinition> MergeFilterScopes(
        IReadOnlyList<FilterDefinition>? parent,
        IReadOnlyList<FilterDefinition> shell,
        params IReadOnlyList<FilterDefinition>[] additional)
    {
        var merged = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase);
        if (parent is not null)
        {
            foreach (var filter in parent)
            {
                merged[filter.Name] = filter;
            }
        }

        foreach (var filter in shell)
        {
            merged[filter.Name] = filter;
        }

        foreach (var scope in additional)
        {
            foreach (var filter in scope)
            {
                merged[filter.Name] = filter;
            }
        }

        return merged.Values.ToList();
    }

    public void AssignToolbarBoard(LayoutBoardDefinition board, string context)
    {
        if (ToolbarBoard is not null)
        {
            throw new DashSpecParseException($"{context} declares more than one toolbar layout board.");
        }

        LayoutModuleScopeValidator.EnsureMatchesIncludeSite(board, LayoutScope.Toolbar, context);
        ToolbarBoard = board;
    }

    public void AssignTabLayoutBoard(LayoutBoardDefinition board, string context)
    {
        if (LayoutBoard is not null)
        {
            throw new DashSpecParseException($"{context} declares more than one card layout board.");
        }

        LayoutModuleScopeValidator.EnsureMatchesIncludeSite(board, LayoutScope.Tab, context);
        LayoutBoard = board;
    }
}

internal static class DashboardShellParser
{
    public static bool TryParseStatement(TokenReader reader, DashboardShellContext ctx)
    {
        if (ctx.Mode is DashboardShellMode.DashboardBody)
        {
            if (TryParseIncludeToolbar(reader, ctx))
            {
                return true;
            }
        }

        if (ctx.Mode is DashboardShellMode.TabModuleStandalone or DashboardShellMode.TabModuleEmbedded)
        {
            if (TryParseIncludeLayout(reader, ctx))
            {
                return true;
            }
        }

        if (reader.TryKeyword("connector"))
        {
            var connectorId = reader.ReadIdent();
            if (ctx.Mode is DashboardShellMode.DashboardBody or DashboardShellMode.TabModuleStandalone)
            {
                ctx.ConnectorId = connectorId;
            }

            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("layout"))
        {
            var grid = LayoutParser.ParseGrid(reader);
            if (ctx.Mode is DashboardShellMode.DashboardBody or DashboardShellMode.TabModuleStandalone)
            {
                ctx.Layout = grid;
            }

            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("palette"))
        {
            var palette = DashboardParser.ReadPaletteReference(reader);
            if (ctx.Mode is DashboardShellMode.DashboardBody or DashboardShellMode.TabModuleStandalone)
            {
                ctx.ColorPalette = palette;
            }

            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("filters") || reader.TryKeyword("toolbar"))
        {
            ParseFiltersChrome(reader, ctx);
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("commands"))
        {
            foreach (var (alias, filterId) in CommandAliasesParser.Parse(reader))
            {
                ctx.CommandAliases[alias] = filterId;
            }

            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("filter"))
        {
            var filter = FilterParser.Parse(reader);
            if (ctx.Mode is DashboardShellMode.TabModuleEmbedded)
            {
                ctx.ShellFilters.Add(filter);
            }
            else
            {
                ctx.Filters.Add(filter);
            }

            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("tab"))
        {
            ParseTabStatement(reader, ctx);
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("phase"))
        {
            var phaseId = reader.ReadIdent();
            if (string.IsNullOrWhiteSpace(phaseId))
            {
                throw new DashSpecParseException("phase requires id.");
            }

            BlockSyntax.BeginBlock(reader);
            reader.SkipNewlines();
            var previousPhase = ctx.CurrentPhaseId;
            ctx.CurrentPhaseId = phaseId;
            while (!BlockSyntax.IsBlockEnd(reader, "phase", phaseId) && !reader.IsEof)
            {
                reader.SkipNewlines();
                if (BlockSyntax.IsBlockEnd(reader, "phase", phaseId))
                {
                    break;
                }

                if (!TryParseStatement(reader, ctx))
                {
                    throw reader.Unexpected();
                }
            }

            BlockSyntax.ExpectBlockEnd(reader, "phase", phaseId);
            ctx.CurrentPhaseId = previousPhase;
            reader.SkipNewlines();
            return true;
        }

        if (reader.TryKeyword("card"))
        {
            ctx.Cards.Add(CardParser.Parse(
                reader,
                ctx.CardBindValidationFilters,
                ctx.SpecDirectory,
                ctx.Includes,
                ctx.ParseOptions,
                ctx.CurrentPhaseId,
                ctx.CurrentPageId));
            reader.SkipNewlines();
            return true;
        }

        return false;
    }

    private static void ParseFiltersChrome(TokenReader reader, DashboardShellContext ctx)
    {
        ParseFiltersChrome(reader, ctx, assign: ctx.Mode is DashboardShellMode.DashboardBody or DashboardShellMode.TabModuleStandalone);
    }

    internal static void ParseFiltersChromePublic(TokenReader reader, DashboardShellContext ctx, bool assign) =>
        ParseFiltersChrome(reader, ctx, assign);

    private static void ParseFiltersChrome(TokenReader reader, DashboardShellContext ctx, bool assign)
    {
        var assignChrome = assign;

        if (reader.TryKeywordSameLine("dashboard"))
        {
            if (assignChrome)
            {
                ToolbarPlacementParser.Parse(reader, ctx, "filters dashboard");
            }
            else
            {
                ToolbarPlacementParser.Discard(reader, "filters dashboard");
            }

            return;
        }

        if (reader.TryKeywordSameLine("chrome"))
        {
            var chrome = FiltersChromeParser.Parse(reader);
            if (assignChrome)
            {
                ctx.FiltersChrome = chrome;
            }

            return;
        }

        if (assignChrome)
        {
            ToolbarPlacementParser.Parse(reader, ctx, "toolbar");
        }
        else
        {
            ToolbarPlacementParser.Discard(reader, "toolbar");
        }
    }

    private static bool TryParseIncludeToolbar(TokenReader reader, DashboardShellContext ctx)
    {
        if (!reader.TryKeyword("include"))
        {
            return false;
        }

        var (kind, reference) = DiagramModuleParser.ReadIncludeReference(reader);
        if (!string.Equals(kind, "toolbar", StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Dashboard shell allows include toolbar only, got include {kind}.");
        }

        if (string.IsNullOrWhiteSpace(ctx.SpecDirectory))
        {
            throw new DashSpecParseException(
                "include toolbar requires spec directory when parsing (path to the .dashspec folder).");
        }

        ctx.AssignToolbarBoard(LayoutModuleParser.Load(reference, ctx.SpecDirectory), "include toolbar");
        reader.SkipNewlines();
        return true;
    }

    private static void ParseTabStatement(TokenReader reader, DashboardShellContext ctx)
    {
        switch (ctx.Mode)
        {
            case DashboardShellMode.DashboardBody:
                ctx.Tabs.Add(TabParser.Parse(reader));
                return;

            case DashboardShellMode.TabModuleStandalone or DashboardShellMode.TabModuleEmbedded:
            {
                if (string.IsNullOrWhiteSpace(ctx.TabModuleId))
                {
                    throw new DashSpecParseException("Tab module shell requires @tab id before tab block.");
                }

                var (moduleLabel, moduleFilters, moduleLayout) =
                    TabParser.ParseModuleLocalBlock(reader, ctx.TabModuleId, allowFilters: true);
                ApplyTabModuleBlock(ctx, moduleLabel, moduleFilters, moduleLayout);
                return;
            }

            default:
                throw new DashSpecParseException("Unexpected tab statement in dashboard shell.");
        }
    }

    private static void ApplyTabModuleBlock(
        DashboardShellContext ctx,
        string? moduleLabel,
        IReadOnlyList<FilterDefinition> moduleFilters,
        LayoutBoardDefinition? moduleLayout)
    {
        ctx.TabModuleLabel ??= moduleLabel;

        if (moduleLayout is not null && ctx.LayoutBoard is not null)
        {
            throw new DashSpecParseException(
                $"Tab module '{ctx.TabModuleId}' declares layout twice: include layout and tab {{ layout }}.");
        }

        ctx.LayoutBoard ??= moduleLayout;

        if (ctx.Mode is DashboardShellMode.TabModuleStandalone)
        {
            ctx.Filters.AddRange(moduleFilters);
            return;
        }

        ctx.TabLocalFilters.AddRange(moduleFilters);
    }

    private static bool TryParseIncludeLayout(TokenReader reader, DashboardShellContext ctx)
    {
        if (!reader.TryKeyword("include"))
        {
            return false;
        }

        var (kind, reference) = DiagramModuleParser.ReadIncludeReference(reader);
        if (!string.Equals(kind, "layout", StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Tab module shell allows include layout only at file top, got include {kind}.");
        }

        if (string.IsNullOrWhiteSpace(ctx.SpecDirectory))
        {
            throw new DashSpecParseException(
                "include layout requires spec directory when parsing (path to the .dashspec folder).");
        }

        if (ctx.LayoutBoard is not null)
        {
            throw new DashSpecParseException("Tab module declares more than one layout board.");
        }

        ctx.AssignTabLayoutBoard(LayoutModuleParser.Load(reference, ctx.SpecDirectory), "include layout");
        reader.SkipNewlines();
        return true;
    }
}
