namespace DashSpec.Modeling.Parse.Layout

open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// Layout board bracket row `[ ref … ]` only — `ref:weight` is **not** generic colon syntax (ADR-0071).
module LayoutBracketRowParser =

    let private readCell (reader: TokenReader) =
        let id = reader.ReadIdent()
        if reader.IsAt TokenKind.Colon then
            reader.Expect TokenKind.Colon
            let weight = reader.ReadIdent()
            $"{id}:{weight}"
        else
            id

    let parse (reader: TokenReader) =
        reader.Expect TokenKind.LBracket
        reader.SkipNewlines()
        let cells = ResizeArray<string>()

        while not (reader.IsAt TokenKind.RBracket) && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsAt TokenKind.RBracket then ()
            else
                cells.Add(readCell reader)
                reader.SkipNewlines()

        reader.Expect TokenKind.RBracket

        if cells.Count = 0 then
            raise (DashSpecParseException("Layout board row [ … ] must list at least one card ref or id."))

        cells :> IReadOnlyList<string>
