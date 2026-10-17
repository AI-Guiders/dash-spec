using DashSpec.Abstractions.Data;

namespace DashSpec.Core.Model;

public sealed record DashboardDocument(
    string Id,
    string Title,
    string? ConnectorId,
    SqlDialect SqlDialect,
    string? DiagramLibraryPath,
    PaletteDefinition? Palette,
    string? ColorPalette,
    LayoutDefinition Layout,
    FiltersChromeDefinition FiltersChrome,
    IReadOnlyList<FilterDefinition> Filters,
    IReadOnlyList<string> DashboardFilters,
    IReadOnlyList<TabDefinition> Tabs,
    IReadOnlyList<CardDefinition> Cards,
    LayoutBoardDefinition? ToolbarBoard = null,
    ModuleExtensionsDefinition? ModuleExtensions = null,
    IReadOnlyDictionary<string, ModuleDiagramDefinition>? ModuleDiagrams = null,
    IReadOnlyDictionary<string, PresentationBlock>? ModuleChartChromePresets = null,
    IReadOnlyDictionary<string, TooltipDefinition>? ModuleTooltips = null,
    IReadOnlyList<ReportPageDefinition>? Pages = null,
    IReadOnlyDictionary<string, string>? CommandAliases = null,
    ReportFormatDefaults? FormatDefaults = null,
    CardsChromeDefinition? CardsChrome = null,
    ReportTimePolicy? TimePolicy = null,
    IReadOnlyDictionary<string, RowTypeSchema>? RowTypeSchemas = null,
    string? DashflowPath = null,
    DashflowModuleDefinition? Dashflow = null)
{
    public CardsChromeDefinition ResolvedCardsChrome => CardsChrome ?? CardsChromeDefinition.Default;
    public ReportFormatDefaults ResolvedFormatDefaults => FormatDefaults ?? ReportFormatDefaults.Empty;
    public ReportTimePolicy ResolvedTimePolicy => TimePolicy ?? ReportTimePolicy.Default;
    public static IReadOnlyDictionary<string, string> EmptyCommandAliases { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> ResolvedCommandAliases =>
        CommandAliases ?? EmptyCommandAliases;
    public static IReadOnlyDictionary<string, ModuleDiagramDefinition> EmptyModuleDiagrams { get; } =
        new Dictionary<string, ModuleDiagramDefinition>(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, PresentationBlock> EmptyModuleChartChromePresets { get; } =
        new Dictionary<string, PresentationBlock>(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, TooltipDefinition> EmptyModuleTooltips { get; } =
        new Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ModuleDiagramDefinition> ResolvedModuleDiagrams =>
        ModuleDiagrams ?? EmptyModuleDiagrams;

    public IReadOnlyDictionary<string, PresentationBlock> ResolvedChartChromePresets =>
        ModuleChartChromePresets ?? EmptyModuleChartChromePresets;

    public IReadOnlyDictionary<string, TooltipDefinition> ResolvedModuleTooltips =>
        ModuleTooltips ?? EmptyModuleTooltips;

    public static IReadOnlyDictionary<string, RowTypeSchema> EmptyRowTypeSchemas { get; } =
        new Dictionary<string, RowTypeSchema>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RowTypeSchema> ResolvedRowTypeSchemas =>
        RowTypeSchemas ?? EmptyRowTypeSchemas;
}

// CardDefinition is owned by DashSpec.Modeling.Parse.Card (F#).
