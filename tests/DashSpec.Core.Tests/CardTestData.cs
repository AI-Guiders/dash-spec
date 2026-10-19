using Microsoft.FSharp.Core;

namespace DashSpec.Core.Tests;

internal static class CardTestData
{
    public static CardDefinition Create(
        string Id,
        string Title,
        DiagramDefinition Diagram,
        DataSourceDefinition DataSource,
        IReadOnlyList<string>? BoundFilters = null,
        IReadOnlyList<string>? LocalFilters = null,
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
        IReadOnlyDictionary<string, CardDiagramSlotDefinition>? DiagramSlots = null,
        DashSpec.Modeling.Parse.Card.CardClickBehaviour? ClickBehaviour = null,
        IReadOnlyList<ExtensionBlockNode>? ExtensionBlocks = null,
        bool LocalFiltersManualApply = false,
        int? LocalFiltersApplySplitIndex = null,
        LayoutBoardDefinition? LocalFiltersChromeBoard = null,
        CardVisibilityRule? Visibility = null,
        string? PhaseId = null,
        string? PageId = null,
        MatrixRenderLimitsDefinition? MatrixLimits = null,
        string? OversizeMessage = null,
        CardChromeDefinition? Chrome = null,
        InspectPresentation? Inspect = null,
        TooltipDefinition? Tooltip = null,
        DashSpec.Modeling.Parse.Card.CardFlowInput? FlowInput = null,
        IReadOnlyDictionary<string, DashSpec.Modeling.Parse.Card.CardFlowInput>? CardInputs = null,
        DashSpec.Modeling.Parse.Card.CardInteriorFlowDefinition? InteriorFlow = null) =>
        new()
        {
            Id = Id,
            Title = Title,
            Diagram = Diagram,
            DataSource = DataSource,
            BoundFilters = BoundFilters ?? [],
            LocalFilters = LocalFilters ?? [],
            Placement = ToOption(Placement),
            TabId = ToOption(TabId),
            LayoutRef = ToOption(LayoutRef),
            UseCardPreset = ToOption(UseCardPreset),
            Legend = ToOption(Legend),
            Presentation = ToOption(Presentation),
            SeriesTransform = ToOption(SeriesTransform),
            FilterHostCardId = ToOption(FilterHostCardId),
            HostedFilters = ToOption(HostedFilters),
            InteriorBoard = ToOption(InteriorBoard),
            DiagramSlotRef = ToOption(DiagramSlotRef),
            DiagramSlots = DiagramSlots ?? new Dictionary<string, CardDiagramSlotDefinition>(),
            ClickBehaviour = ToOption(ClickBehaviour),
            ExtensionBlocks = ExtensionBlocks ?? [],
            LocalFiltersManualApply = LocalFiltersManualApply,
            LocalFiltersApplySplitIndex = ToOption(LocalFiltersApplySplitIndex),
            LocalFiltersChromeBoard = ToOption(LocalFiltersChromeBoard),
            Visibility = ToOption(Visibility),
            PhaseId = ToOption(PhaseId),
            PageId = ToOption(PageId),
            MatrixLimits = ToOption(MatrixLimits),
            OversizeMessage = ToOption(OversizeMessage),
            Chrome = ToOption(Chrome),
            Inspect = ToOption(Inspect),
            Tooltip = ToOption(Tooltip),
            FlowInput = ToOption(FlowInput),
            CardInputs = CardInputs ?? new Dictionary<string, DashSpec.Modeling.Parse.Card.CardFlowInput>(),
            InteriorFlow = ToOption(InteriorFlow),
        };

    private static FSharpOption<T> ToOption<T>(T? value) where T : class =>
        value is null ? FSharpOption<T>.None : FSharpOption<T>.Some(value);

    private static FSharpOption<T> ToOption<T>(T? value) where T : struct =>
        value is T present ? FSharpOption<T>.Some(present) : FSharpOption<T>.None;

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
