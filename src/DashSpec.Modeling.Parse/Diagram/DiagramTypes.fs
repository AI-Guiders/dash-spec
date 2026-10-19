namespace DashSpec.Modeling.Parse.Diagram

open System.Collections.Generic
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

[<CLIMutable>]
type DiagramDefinition =
    { Kind: string
      Properties: IReadOnlyDictionary<string, string>
      UsePreset: string option }

[<CLIMutable>]
type InspectPresentation =
    { TooltipId: string option
      Label: string option
      Format: string
      Split: string }

type DiagramFragmentStatement =
    | DiagramStatement of DiagramDefinition
    | PresentationStatement of PresentationBlock
    | SeriesTransformStatement of SeriesTransformBlock
    | TooltipStatement of string * TooltipDefinition
    | InspectStatement of InspectPresentation

[<CLIMutable>]
type DiagramModuleDocument =
    { Id: string
      Statements: IReadOnlyList<DiagramFragmentStatement> }
