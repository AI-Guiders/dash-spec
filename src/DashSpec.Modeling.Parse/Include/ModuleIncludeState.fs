namespace DashSpec.Modeling.Parse.Include

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip

type ModuleIncludeState() =
    let diagrams = Dictionary<string, SpecIncludeFragment>(StringComparer.OrdinalIgnoreCase)
    let chartChromePresets = Dictionary<string, PresentationBlock>(StringComparer.OrdinalIgnoreCase)
    let tooltips = Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)

    let mutable layoutBoard: LayoutBoardDefinition option = None
    let mutable toolbarBoard: LayoutBoardDefinition option = None

    member _.LayoutBoard = layoutBoard
    member _.ToolbarBoard = toolbarBoard

    member _.TryGetDiagram(id: string, fragment: outref<SpecIncludeFragment>) =
        match diagrams.TryGetValue id with
        | true, value ->
            fragment <- value
            true
        | false, _ -> false

    member this.RegisterDiagram(id: string, fragment: SpecIncludeFragment) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Diagram id is required."
        if diagrams.ContainsKey id then
            raise (DashSpecParseException($"Duplicate diagram id '{id}' in module includes."))
        diagrams.[id] <- fragment

    member this.RegisterTooltip(id: string, definition: TooltipDefinition) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Tooltip id is required."
        if tooltips.ContainsKey id then
            raise (DashSpecParseException($"Duplicate tooltip id '{id}' in module includes."))
        tooltips.[id] <- definition

    member this.RegisterChartChromePreset(id: string, block: PresentationBlock) =
        if String.IsNullOrWhiteSpace id then invalidArg "id" "Preset id is required."
        if chartChromePresets.ContainsKey id then
            raise (DashSpecParseException($"Duplicate chart chrome preset '{id}' in module includes."))
        chartChromePresets.[id] <- block

    member this.AssignLayoutBoard(board: LayoutBoardDefinition, context: string) =
        if layoutBoard.IsSome then
            raise (DashSpecParseException($"{context} declares more than one card layout board."))
        LayoutModuleScopeValidator.ensureMatchesIncludeSite board LayoutScope.Tab context
        layoutBoard <- Some board

    member this.AssignToolbarBoard(board: LayoutBoardDefinition, context: string) =
        if toolbarBoard.IsSome then
            raise (DashSpecParseException($"{context} declares more than one toolbar layout board."))
        LayoutModuleScopeValidator.ensureMatchesIncludeSite board LayoutScope.Toolbar context
        toolbarBoard <- Some board

    member _.ExportChartChromePresets() = chartChromePresets :> IReadOnlyDictionary<_, _>
    member _.ExportTooltips() = tooltips :> IReadOnlyDictionary<_, _>
    member _.Diagrams = diagrams :> IReadOnlyDictionary<_, _>
