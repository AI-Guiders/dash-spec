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
