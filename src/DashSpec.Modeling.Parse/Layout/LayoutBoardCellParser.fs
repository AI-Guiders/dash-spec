namespace DashSpec.Modeling.Parse.Layout

open System
open DashSpec.Modeling.Core

/// <summary>Board cell token with optional <c>:weight</c> suffix (ADR-0061).</summary>
module LayoutBoardCellParser =

    let refToken (cell: string) =
        if String.IsNullOrWhiteSpace cell then
            raise (DashSpecParseException("Layout board cell is empty."))

        let separator = cell.IndexOf(':')
        if separator < 0 then cell
        elif separator = 0 || separator = cell.Length - 1 then
            raise (DashSpecParseException($"Layout board cell '{cell}' must use form ref:weight."))
        else
            cell.Substring(0, separator)

    /// <summary>Parse <c>ref</c> or <c>ref:weight</c> (ADR-0061); mirrors Core <c>LayoutBoardRowPlacer.ParseCell</c>.</summary>
    let parseCell (token: string) (context: string) (gridRow: int) =
        if String.IsNullOrWhiteSpace token then
            raise (DashSpecParseException($"{context}: row {gridRow} has an empty cell."))

        let separator = token.IndexOf(':')
        if separator < 0 then
            token, 1
        elif separator = 0 || separator = token.Length - 1 then
            raise (DashSpecParseException($"{context}: row {gridRow} cell '{token}' must use form ref:weight."))
        else
            let refToken = token.Substring(0, separator)
            let weightText = token.Substring(separator + 1)

            match Int32.TryParse weightText with
            | true, weight when weight > 0 -> refToken, weight
            | _ ->
                raise (DashSpecParseException($"{context}: row {gridRow} cell '{token}' requires a positive integer weight."))
