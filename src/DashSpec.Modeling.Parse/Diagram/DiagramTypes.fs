namespace DashSpec.Modeling.Parse.Diagram

open System.Collections.Generic
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Syntax
open DashSpec.Modeling.Parse.Tooltip
open DashSpec.Modeling.Parse.Transform

[<CLIMutable>]
type DiagramDefinition =
    { Kind: string
      Properties: IReadOnlyDictionary<string, string>
      UsePreset: string option }

module TableColumnsBridge =

    let private table =
        System.Runtime.CompilerServices.ConditionalWeakTable<DiagramDefinition, IReadOnlyList<ColumnRowSyntax>>()

    let attach (diagram: DiagramDefinition) (columns: IReadOnlyList<ColumnRowSyntax>) =
        if not (isNull (box diagram)) && not (isNull columns) && columns.Count > 0 then
            table.Remove(diagram) |> ignore
            table.Add(diagram, columns)
        diagram

    let get (diagram: DiagramDefinition) : IReadOnlyList<ColumnRowSyntax> =
        if isNull (box diagram) then
            [||] :> IReadOnlyList<ColumnRowSyntax>
        else
            match table.TryGetValue(diagram) with
            | true, cols -> cols
            | false, _ -> [||] :> IReadOnlyList<ColumnRowSyntax>

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
