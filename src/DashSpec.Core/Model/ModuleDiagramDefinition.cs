namespace DashSpec.Core.Model;

/// <summary>Diagram preset registered via <c>import diagrams from &lt;namespace&gt;</c> in the tab/dashboard header.</summary>
public sealed record ModuleDiagramDefinition(
    DiagramDefinition Diagram,
    PresentationBlock? Presentation,
    SeriesTransformBlock? SeriesTransform,
    InspectPresentation? Inspect = null,
    TooltipDefinition? Tooltip = null);
