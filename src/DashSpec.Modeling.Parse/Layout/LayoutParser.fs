namespace DashSpec.Modeling.Parse.Layout

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module LayoutParser =

    let private parseBoardRow (reader: TokenReader) =
        reader.Expect TokenKind.LBracket
        reader.SkipNewlines()
        let cells = ResizeArray<string>()
        while not (reader.IsAt TokenKind.RBracket) && not reader.IsEof do
            reader.SkipNewlines()
            if reader.IsAt TokenKind.RBracket then ()
            else
                cells.Add(reader.ReadIdent())
                reader.SkipNewlines()
        reader.Expect TokenKind.RBracket
        if cells.Count = 0 then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout board row [ … ] must list at least one card ref or id."))
        cells :> IReadOnlyList<string>

    let private parseGroupBlock (reader: TokenReader) =
        let groupId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace groupId then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout group requires an id."))

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let mutable groupTitle: string option = None
        let rows = ResizeArray<IReadOnlyList<string>>()

        while not reader.IsEof && not (BlockSyntax.isBlockEnd reader "group" (Some groupId)) do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "group" (Some groupId) then ()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                groupTitle <- Some(reader.ReadString())
            elif reader.IsAt TokenKind.LBracket then
                rows.Add(parseBoardRow reader)
            else
                raise (reader.Unexpected("[ or title"))

        BlockSyntax.expectBlockEnd reader "group" (Some groupId)

        if rows.Count = 0 then
            raise (DashSpec.Modeling.Core.DashSpecParseException($"Layout group '{groupId}' requires at least one row [ … ]."))

        GroupRow
            { Id = groupId
              Title = groupTitle
              Rows = rows :> IReadOnlyList<_> }

    let private parseBoardEntry (reader: TokenReader) =
        if reader.IsAt TokenKind.LBracket then CardRow(parseBoardRow reader)
        elif reader.TryKeyword "group" then parseGroupBlock reader
        else raise (reader.Unexpected("[ or group"))

    /// Bracket rows and optional groups until EOF or end kind (for .dashlayout modules).
    let parseBoardRows (reader: TokenReader) (endKind: string option) (endId: string option) =
        let entries = ResizeArray<LayoutBoardEntry>()
        reader.SkipNewlines()
        while not reader.IsEof
              && not (reader.IsAt TokenKind.RBrace)
              && (endKind.IsNone || not (BlockSyntax.isBlockEnd reader endKind.Value endId)) do
            entries.Add(parseBoardEntry reader)
            reader.SkipNewlines()
        if entries.Count = 0 then
            raise (DashSpec.Modeling.Core.DashSpecParseException("Layout board requires at least one row [ … ] or group { … }."))
        { Entries = entries :> IReadOnlyList<_>; ModuleScope = None }

    let parseGrid (reader: TokenReader) =
        match reader.ReadIdent() with
        | "grid" -> ()
        | _ -> raise (reader.Unexpected("grid"))

        let props = PropertyBlockParser.parse reader PropertySchemas.layoutGrid "layout grid" false false
        let columns =
            match props.TryGetValue "columns" with
            | true, raw ->
                let mutable parsed = 0
                if System.Int32.TryParse(raw, &parsed) && parsed > 0 && parsed <= 24 then parsed
                else LayoutDefinition.Default.Columns
            | false, _ -> LayoutDefinition.Default.Columns

        let gapPx =
            match props.TryGetValue "gap" with
            | true, raw ->
                let mutable parsed = 0
                if System.Int32.TryParse(raw, &parsed) && parsed >= 0 then parsed
                else LayoutDefinition.Default.GapPx
            | false, _ -> LayoutDefinition.Default.GapPx

        { Columns = columns; GapPx = gapPx }

    let parseBoard (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let board = parseBoardRows reader (Some "layout") None
        BlockSyntax.expectBlockEnd reader "layout" None
        board

    let parseSpanValue (value: string) =
        match value.ToLowerInvariant() with
        | "full" -> 12
        | "half" -> 6
        | "third" -> 4
        | _ ->
            let mutable parsed = 0
            if Int32.TryParse(value, &parsed) && parsed > 0 then parsed else 6

    let parsePlacement (reader: TokenReader) =
        let props =
            PropertyBlockParser.parse reader PropertySchemas.placement "place" false false

        let row =
            match props.TryGetValue "row" with
            | true, raw ->
                let mutable parsed = 0
                if Int32.TryParse(raw, &parsed) && parsed > 0 then parsed else 1
            | false, _ -> 1

        let col =
            match props.TryGetValue "col" with
            | true, raw ->
                let mutable parsed = 0
                if Int32.TryParse(raw, &parsed) && parsed > 0 then parsed else 1
            | false, _ -> 1

        let span =
            match props.TryGetValue "span" with
            | true, raw -> parseSpanValue raw
            | false, _ -> 6

        { Row = row; Col = col; Span = span }

    /// Bracket rows until EOF or end kind (string overload for card/toolbar parsers).
    let parseBoardRowsForEndKind (reader: TokenReader) (endKind: string) =
        parseBoardRows reader (Some endKind) None