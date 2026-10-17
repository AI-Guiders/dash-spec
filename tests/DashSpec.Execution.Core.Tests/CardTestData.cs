using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Core.Tests;

internal static class CardTestData
{
    public static CardDefinition Create(
        string id,
        string title,
        DiagramDefinition diagram,
        DataSourceDefinition dataSource,
        IReadOnlyList<string> boundFilters,
        IReadOnlyList<string> localFilters,
        CardFlowInputDefinition? flowInput = null,
        string? diagramSlotRef = null,
        IReadOnlyDictionary<string, CardDiagramSlotDefinition>? diagramSlots = null) =>
        new()
        {
            Id = id,
            Title = title,
            Diagram = diagram,
            DataSource = dataSource,
            BoundFilters = boundFilters,
            LocalFilters = localFilters,
            Placement = FSharpOption<PlacementDefinition>.None,
            TabId = FSharpOption<string>.None,
            LayoutRef = FSharpOption<string>.None,
            UseCardPreset = FSharpOption<string>.None,
            Legend = FSharpOption<LegendDefinition>.None,
            Presentation = FSharpOption<PresentationBlock>.None,
            SeriesTransform = FSharpOption<SeriesTransformBlock>.None,
            FilterHostCardId = FSharpOption<string>.None,
            HostedFilters = FSharpOption<IReadOnlyList<string>>.None,
            InteriorBoard = FSharpOption<LayoutBoardDefinition>.None,
            DiagramSlotRef = ToOption(diagramSlotRef),
            DiagramSlots = diagramSlots ?? new Dictionary<string, CardDiagramSlotDefinition>(),
            ClickBehaviour = FSharpOption<CardClickBehaviour>.None,
            ExtensionBlocks = [],
            LocalFiltersManualApply = false,
            LocalFiltersApplySplitIndex = FSharpOption<int>.None,
            LocalFiltersChromeBoard = FSharpOption<LayoutBoardDefinition>.None,
            Visibility = FSharpOption<CardVisibilityRule>.None,
            PhaseId = FSharpOption<string>.None,
            PageId = FSharpOption<string>.None,
            MatrixLimits = FSharpOption<MatrixRenderLimitsDefinition>.None,
            OversizeMessage = FSharpOption<string>.None,
            Chrome = FSharpOption<CardChromeDefinition>.None,
            Inspect = FSharpOption<InspectPresentation>.None,
            Tooltip = FSharpOption<TooltipDefinition>.None,
            FlowInput = ToOption(flowInput),
            CardInputs = new Dictionary<string, CardFlowInputDefinition>(),
            InteriorFlow = FSharpOption<CardInteriorFlowDefinition>.None,
        };

    private static FSharpOption<T> ToOption<T>(T? value) where T : class =>
        value is null ? FSharpOption<T>.None : FSharpOption<T>.Some(value);

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
