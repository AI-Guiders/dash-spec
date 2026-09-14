namespace DashSpec.Modeling.Parse.Card

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module CardLimitsParser =

    let parse (reader: TokenReader) (cardId: string) =
        let props =
            PropertyBlockParser.parse
                reader
                PropertySchemas.cardLimits
                $"card '{cardId}' limits"
                false
                false

        let mutable maxCells: int option = None
        let mutable maxAxisLabels: int option = None

        match props.TryGetValue "cells" with
        | true, cellsRaw ->
            let mutable cells = 0
            if not (Int32.TryParse(cellsRaw, &cells) && cells > 0) then
                raise (DashSpecParseException($"Card '{cardId}': limits.cells must be a positive integer."))
            maxCells <- Some cells
        | false, _ -> ()

        match props.TryGetValue "axis" with
        | true, axisRaw ->
            let mutable axis = 0
            if not (Int32.TryParse(axisRaw, &axis) && axis > 0) then
                raise (DashSpecParseException($"Card '{cardId}': limits.axis must be a positive integer."))
            maxAxisLabels <- Some axis
        | false, _ -> ()

        if maxCells.IsNone && maxAxisLabels.IsNone then
            raise (DashSpecParseException($"Card '{cardId}': limits block must set cells and/or axis."))

        { MaxCells = maxCells; MaxAxisLabels = maxAxisLabels }
