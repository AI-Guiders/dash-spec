namespace DashSpec.Core.Model;

/// <summary>Diagram preset registered via tab/dashboard <c>!include "*.dashdiagram"</c>.</summary>
public sealed record ModuleDiagramDefinition(
    DiagramDefinition Diagram,
    PresentationBlock? Presentation,
    SeriesTransformBlock? SeriesTransform);
