namespace DashSpec.Modeling.Parse.Layout

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module LayoutParser =

    let private layoutGroupMembers (rows: ResizeArray<IReadOnlyList<string>>) =
        [ BlockGrammar.RepeatingBracketRows rows
          BlockGrammar.SchemaProperties(PropertySchemas.layoutGroup, false, false) ]

    let private layoutNestMembers (rows: ResizeArray<IReadOnlyList<string>>) =
        [ BlockGrammar.RepeatingBracketRows rows ]

    let private parseGroupBlock (reader: TokenReader) =
        let groupId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace groupId then
            raise (DashSpecParseException("Layout group requires an id."))

        let rows = ResizeArray<IReadOnlyList<string>>()
        let blockName = $"layout group '{groupId}'"

        let props =
            BlockGrammar.parseKeywordContainer
                reader
                "group"
                blockName
                (layoutGroupMembers rows)
                (Some groupId)

        if rows.Count = 0 then
            raise (DashSpecParseException($"Layout group '{groupId}' requires at least one row [ … ]."))

        let groupTitle =
            match props.TryGetValue "title" with
            | true, title when not (String.IsNullOrWhiteSpace title) -> Some title
            | _ -> None

        GroupRow
            { Id = groupId
              Title = groupTitle
              Rows = rows :> IReadOnlyList<_> }

    let private parseNestBlock (reader: TokenReader) =
        let nestId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace nestId then
            raise (DashSpecParseException("Layout nest requires an id."))

        let rows = ResizeArray<IReadOnlyList<string>>()
        let blockName = $"layout nest '{nestId}'"

        BlockGrammar.parseKeywordContainer reader "nest" blockName (layoutNestMembers rows) (Some nestId)
        |> ignore

        if rows.Count = 0 then
            raise (DashSpecParseException($"Layout nest '{nestId}' requires at least one row [ … ]."))

        NestRow { Id = nestId; Rows = rows :> IReadOnlyList<_> }

    let private parseBoardEntry (reader: TokenReader) =
        if reader.IsAt TokenKind.LBracket then CardRow(MemberGrammar.parseBoardRow reader)
        elif reader.TryKeyword "group" then parseGroupBlock reader
        elif reader.TryKeyword "nest" then parseNestBlock reader
        else raise (reader.Unexpected("[, group, or nest"))

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
            raise (DashSpecParseException("Layout board requires at least one row [ … ], group { … }, or nest { … }."))
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
