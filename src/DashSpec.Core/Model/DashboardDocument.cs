namespace DashSpec.Core.Model;

public sealed record DashboardDocument(
    string Id,
    string Title,
    string? ConnectorId,
    SqlDialect SqlDialect,
    string? DiagramLibraryPath,
    string? PalettePath,
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
    IReadOnlyDictionary<string, string>? CommandAliases = null)
{
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
}

public sealed record FilterDefinition(
    FilterKind Kind,
    string Name,
    string? DefaultExpression,
    string? ColumnReference,
    string? Label = null,
    string? Widget = null,
    int? MinValue = null,
    int? MaxValue = null,
    string? GrainFilterName = null,
    bool SingleSelect = false,
    string? LayoutRef = null,
    IReadOnlyDictionary<string, string>? GrainLabels = null)
{
    public bool IsDayWidget =>
        string.Equals(Widget, "day", StringComparison.OrdinalIgnoreCase);

    public bool IsComboboxWidget =>
        string.Equals(Widget, "combobox", StringComparison.OrdinalIgnoreCase);

    public bool IsSelectWidget =>
        string.Equals(Widget, "select", StringComparison.OrdinalIgnoreCase);

    public bool IsSingleSelectField =>
        SingleSelect || IsSelectWidget;
}

public enum FilterKind
{
    Date,
    Field,
    Top,
}

public sealed record CardDefinition(
    string Id,
    string Title,
    DiagramDefinition Diagram,
    DataSourceDefinition DataSource,
    IReadOnlyList<string> BoundFilters,
    IReadOnlyList<string> LocalFilters,
    PlacementDefinition? Placement = null,
    string? TabId = null,
    string? LayoutRef = null,
    string? UseCardPreset = null,
    LegendDefinition? Legend = null,
    PresentationBlock? Presentation = null,
    SeriesTransformBlock? SeriesTransform = null,
    string? FilterHostCardId = null,
    IReadOnlyList<string>? HostedFilters = null,
    LayoutBoardDefinition? InteriorBoard = null,
    string? DiagramSlotRef = null,
    CardClickBehaviour? ClickBehaviour = null,
    IReadOnlyList<ExtensionBlockNode> ExtensionBlocks = null!,
    bool LocalFiltersManualApply = false,
    CardVisibilityRule? Visibility = null,
    string? PhaseId = null,
    string? PageId = null,
    MatrixRenderLimitsDefinition? MatrixLimits = null,
    string? OversizeMessage = null,
    CardChromeDefinition? Chrome = null,
    InspectPresentation? Inspect = null,
    TooltipDefinition? Tooltip = null);
public sealed record DiagramDefinition(
    string Kind,
    IReadOnlyDictionary<string, string> Properties,
    string? UsePreset = null);

public sealed record DataSourceDefinition(
    DataSourceKind Kind,
    string Value,
    DataSourceSqlCarrier? SqlCarrier = null);

public enum DataSourceKind
{
    View,
    Sql,
}

public enum DataSourceSqlCarrier
{
    Query,
    File,
}
