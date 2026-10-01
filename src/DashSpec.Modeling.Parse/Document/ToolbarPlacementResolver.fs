namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Layout

module ToolbarPlacementResolver =

    let private boardFromCardRows (rows: IReadOnlyList<IReadOnlyList<string>>) =
        { Entries = rows |> Seq.map LayoutBoardEntry.CardRow |> Seq.toList :> IReadOnlyList<_>
          ModuleScope = None }

    let resolveFilterNames
        (filters: IReadOnlyList<FilterDefinition>)
        (flatNames: IReadOnlyList<string>)
        (board: LayoutBoardDefinition option)
        =
        match board with
        | None -> flatNames
        | Some b ->
            if flatNames.Count > 0 then
                raise (DashSpecParseException("Toolbar cannot combine a layout board with a flat filter list."))

            let context = "Toolbar"
            let names = ResizeArray<string>()
            let gridRow = 1

            for row in b.Rows do
                for token in row do
                    let boardRef, _ = LayoutBoardCellParser.parseCell token context gridRow
                    let name = FilterLayoutRefResolver.resolve boardRef filters context

                    if names |> Seq.exists (fun n -> String.Equals(n, name, StringComparison.OrdinalIgnoreCase)) then
                        raise (DashSpecParseException($"{context}: filter '{name}' appears more than once in the toolbar board."))

                    names.Add name

            if names.Count = 0 then
                raise (DashSpecParseException($"{context} layout board requires at least one filter."))

            names :> IReadOnlyList<_>

    /// <summary>Interop entry when only toolbar card rows are available (Core <c>LayoutBoardDefinition.Rows</c>).</summary>
    let resolveFilterNamesFromRows
        (filters: IReadOnlyList<FilterDefinition>)
        (flatNames: IReadOnlyList<string>)
        (rows: IReadOnlyList<IReadOnlyList<string>> option)
        =
        match rows with
        | None -> flatNames
        | Some cardRows -> resolveFilterNames filters flatNames (Some(boardFromCardRows cardRows))
