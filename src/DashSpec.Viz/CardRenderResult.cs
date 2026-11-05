using DashSpec.Execution.Runtime;

namespace DashSpec.Viz;

public sealed record CardRenderResult(
    string Id,
    string Title,
    string DiagramKind,
    DiagramDataFamily DataFamily,
    string RenderPluginId,
    ChartPayload? Chart = null,
    ChartPayload? DetailChart = null,
    TablePayload? Table = null,
    string? Number = null,
    string? NumberScale = null,
    string? NumberDelta = null,
    string? NumberDeltaTone = null,
    string? Error = null,
    bool Loading = false,
    IReadOnlyList<string>? BoundFilters = null,
    IReadOnlyList<string>? LocalFilters = null,
    PlacementDefinition? Placement = null,
    IReadOnlyDictionary<string, PlacementDefinition>? InteriorPlacements = null,
    ChartPresentation? ChartPresentation = null,
    MatrixPayload? Matrix = null,
    MatrixPayload? DetailMatrix = null,
    GanttPayload? Gantt = null,
    MatrixPresentation? MatrixPresentation = null,
    TablePresentation? TablePresentation = null,
    ReportFormatDefaults? FormatDefaults = null,
    DashSpec.Modeling.Parse.Card.CardClickBehaviour? ClickBehaviour = null,
    IReadOnlyList<ExtensionBlockNode> ExtensionBlocks = null!,
    bool LocalFiltersManualApply = false,
    int? LocalFiltersApplySplitIndex = null,
    IReadOnlyDictionary<string, PlacementDefinition>? LocalFilterChromePlacements = null,
    bool IsVisibilityPlaceholder = false,
    string? VisibilityMessage = null,
    MatrixRenderLimitsDefinition? MatrixLimits = null,
    string? OversizeMessage = null,
    string? EmptyMessage = null,
    string? FilterLinkHint = null,
    string? FilterLinkCssClass = null,
    string? TopFilterScopeHint = null,
    bool ShowChromeTitle = true,
    /// <summary>The card's chrome — the domain definition passed through (fold,
    /// slot_expand, bound_filters, hide_title), not splatted into loose fields.
    /// ShowChromeTitle stays separate: it is the resolved title bit (catalog-prod
    /// suppression, ResolvedCardChrome) — not expressible from the definition alone.</summary>
    CardChromeDefinition? Chrome = null,
    IReadOnlyDictionary<string, CardSlotRenderResult>? InteriorSlotRenders = null,
    IReadOnlyList<DashSpec.Modeling.Parse.Card.CardClickHandler>? ClickHandlers = null,
    /// <summary>Граф (G), которым получены данные карточки: клик-значения конвертируются его инверсией.</summary>
    DashflowExecutionPath? FlowPath = null)
{
    public bool HasExpandedPayload => DetailChart is not null || DetailMatrix is not null;

    public CardRenderResult ForView(bool detailView) =>
        !detailView || !HasExpandedPayload
            ? this
            : this with
            {
                Chart = DetailChart ?? Chart,
                Matrix = DetailMatrix ?? Matrix,
            };
}
