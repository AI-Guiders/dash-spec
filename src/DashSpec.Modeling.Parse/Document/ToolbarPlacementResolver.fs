namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Layout

/// Simplified resolver — full FilterLayoutRefResolver pending port.
module ToolbarPlacementResolver =

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

            let names = ResizeArray<string>()

            for row in b.Rows do
                for token in row do
                    let name =
                        filters
                        |> Seq.tryFind (fun f ->
                            String.Equals(f.Name, token, StringComparison.OrdinalIgnoreCase)
                            || (f.LayoutRef.IsSome
                                && String.Equals(f.LayoutRef.Value, token, StringComparison.OrdinalIgnoreCase)))
                        |> function
                            | Some f -> f.Name
                            | None -> token

                    if names |> Seq.exists (fun n -> String.Equals(n, name, StringComparison.OrdinalIgnoreCase)) then
                        raise (DashSpecParseException($"Toolbar: filter '{name}' appears more than once in the toolbar board."))

                    names.Add name

            if names.Count = 0 then
                raise (DashSpecParseException("Toolbar layout board requires at least one filter."))

            names :> IReadOnlyList<_>
