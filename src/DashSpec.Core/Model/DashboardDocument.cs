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
    LayoutBoardDefinition? ToolbarBoard = null);

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
    string? LayoutRef = null)
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
    SeriesTransformBlock? SeriesTransform = null);

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
