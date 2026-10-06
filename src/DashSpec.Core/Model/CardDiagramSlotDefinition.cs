namespace DashSpec.Core.Model;

/// <summary>One diagram + data binding unit inside a card interior board.</summary>
public sealed record CardDiagramSlotDefinition(
    string SlotRef,
    DiagramDefinition Diagram,
    DataSourceDefinition DataSource,
    IReadOnlyList<string> BoundFilters,
    LegendDefinition? Legend = null,
    PresentationBlock? Presentation = null,
    SeriesTransformBlock? SeriesTransform = null,
    CardFlowInputDefinition? FlowInput = null);
