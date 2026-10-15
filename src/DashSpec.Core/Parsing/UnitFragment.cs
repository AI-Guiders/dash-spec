using DashSpec.Core.Authoring;
using DashSpec.Core.Model;
using DashSpec.Modeling.Parse.Import;

namespace DashSpec.Core.Parsing;

/// <summary>Resolved diagram / presentation / transform / tooltip fragment of one unit file.</summary>
internal sealed record UnitFragment(
    DiagramDefinition? Diagram,
    PresentationBlock? Presentation,
    SeriesTransformBlock? SeriesTransform,
    IReadOnlyDictionary<string, TooltipDefinition>? Tooltips = null,
    InspectPresentation? Inspect = null);
