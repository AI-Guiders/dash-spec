namespace DashSpec.Modeling.Parse.Include

open System.Collections.Generic
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

/// Resolved diagram / presentation / transform / tooltip fragment from an include file.
[<CLIMutable>]
type SpecIncludeFragment =
    { Diagram: DiagramDefinition option
      Presentation: PresentationBlock option
      SeriesTransform: SeriesTransformBlock option
      Tooltips: IReadOnlyDictionary<string, TooltipDefinition> option
      Inspect: InspectPresentation option }
