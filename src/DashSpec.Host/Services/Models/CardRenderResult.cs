using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Services.Models;

public sealed record CardRenderResult(
    string Id,
    string Title,
    string DiagramKind,
    DiagramDataFamily DataFamily,
    string RenderPluginId,
    ChartPayload? Chart = null,
    TablePayload? Table = null,
    string? Number = null,
    string? Error = null,
    bool Loading = false,
    IReadOnlyList<string>? BoundFilters = null,
    IReadOnlyList<string>? LocalFilters = null,
    PlacementDefinition? Placement = null,
    IReadOnlyDictionary<string, PlacementDefinition>? InteriorPlacements = null,
    ChartPresentation? ChartPresentation = null,
    MatrixPayload? Matrix = null,
    MatrixPresentation? MatrixPresentation = null,
    CardClickBehaviour? ClickBehaviour = null,
    IReadOnlyList<ExtensionBlockNode> ExtensionBlocks = null!,
    bool LocalFiltersManualApply = false,
    bool IsVisibilityPlaceholder = false,
    string? VisibilityMessage = null,
    MatrixRenderLimitsDefinition? MatrixLimits = null,
    string? OversizeMessage = null,
    string? FilterLinkHint = null,
    string? FilterLinkCssClass = null,
    string? TopFilterScopeHint = null);
