namespace DashSpec.Modeling.Parse

open System
open System.Collections.Generic
open System.Text
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// Composable block containers: `keyword` … `end keyword` and `{ … }` (ADR-0036, ADR-0071).
module BlockGrammar =

    type StringMapMerge =
        { IrProperty: string
          Serialize: IReadOnlyDictionary<string, string> -> string }

    type ContainerMember =
        | SchemaProperties of
            schema: PropertySchemas.PropertySpec list * allowExtension: bool * allowQuotedKeys: bool
        | ChildKeywordMerge of
            keyword: string * endKind: string * blockName: string * readValue: (TokenReader -> string) * merge: StringMapMerge
        | ChildKeyword of keyword: string * parse: (TokenReader -> unit)
        | KeywordScalarOrBlock of
            keyword: string * endKind: string * blockName: string * onScalar: (TokenReader -> Dictionary<string, string> -> unit) * onBlock: (TokenReader -> Dictionary<string, string> -> unit)
        | RepeatingBracketRows of
            rows: ResizeArray<IReadOnlyList<string>> * parseRow: (TokenReader -> IReadOnlyList<string>)
        /// Bare `[] [] []` empty-cell rows at container level ([ADR-0111] §2: the grid is drawn with empty cells).
        /// Each row collects its length — how many `[]` tokens before the line ends.
        | RepeatingEmptyCellRows of rows: ResizeArray<int>
        /// Bare `| a | b |` table rows at container level (GDL-table body; ADR-0004 deferred step family).
        /// Collected verbatim; header row + cell semantics are the consumer's (TableLiteral).
        | TableRowRows of rows: ResizeArray<string>

    let serializeColumnFormatMap (map: IReadOnlyDictionary<string, string>) =
        let sb = StringBuilder()
        for kv in map do
            if sb.Length > 0 then
                sb.Append(", ") |> ignore
            sb.Append(kv.Key).Append(':').Append(kv.Value.Trim()) |> ignore
        sb.ToString()

    let tableFormatsMerge =
        { IrProperty = "column_formats"
          Serialize = serializeColumnFormatMap }

    /// Column labels ride on newline-joined `id = label` lines so commas inside a label survive the IR round-trip.
    let serializeColumnLabelMap (map: IReadOnlyDictionary<string, string>) =
        let sb = StringBuilder()
        for kv in map do
            if sb.Length > 0 then
                sb.Append('\n') |> ignore
            sb.Append(kv.Key).Append(" = ").Append(kv.Value.Trim()) |> ignore
        sb.ToString()

    /// <summary>
    /// <c>| a | b |</c> TableRow rows until <c>end endKind</c> — GDL-table block body (ADR-0004 deferred step).
    /// Separator rows (<c>|---|---|</c>) are left for <see cref="TableLiteral"/> to skip.
    /// </summary>
    let readPipeTableRows (reader: TokenReader) (endKind: string) (blockName: string) : string list =
        let rows = ResizeArray<string>()
        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()
            if not (BlockSyntax.isBlockEnd reader endKind None) then
                if reader.CurrentKind <> TokenKind.TableRow then
                    raise (reader.Unexpected($"table row '| a | b |' inside {blockName}"))
                rows.Add(reader.CurrentTableRow())
                reader.Advance()
        BlockSyntax.expectBlockEnd reader endKind None
        List.ofSeq rows

    let rec private tryParseMember (reader: TokenReader) (memberDef: ContainerMember) (values: Dictionary<string, string>) =
        match memberDef with
        | RepeatingBracketRows(rows, parseRow) when reader.IsAt TokenKind.LBracket ->
            rows.Add(parseRow reader)
            true
        | RepeatingEmptyCellRows rows when reader.IsAt TokenKind.EmptyCell ->
            let mutable count = 0
            while reader.IsAt TokenKind.EmptyCell do
                reader.Advance()
                count <- count + 1
            rows.Add(count)
            true
        | TableRowRows rows when reader.CurrentKind = TokenKind.TableRow ->
            rows.Add(reader.CurrentTableRow())
            reader.Advance()
            true
        | _ -> tryParseChild reader memberDef values

    and private tryParseChild (reader: TokenReader) (memberDef: ContainerMember) (values: Dictionary<string, string>) =
        match memberDef with
        | RepeatingBracketRows _ -> false
        | RepeatingEmptyCellRows _ -> false
        | TableRowRows _ -> false
        | ChildKeywordMerge(keyword, endKind, blockName, readValue, merge) ->
            if reader.TryKeyword keyword then
                let map = MemberGrammar.parseStringMapBlock reader endKind blockName readValue
                values.[merge.IrProperty] <- merge.Serialize map
                true
            else
                false
        | ChildKeyword(keyword, parseChild) ->
            if reader.TryKeyword keyword then
                parseChild reader
                true
            else
                false
        | KeywordScalarOrBlock(keyword, endKind, blockName, onScalar, onBlock) ->
            if reader.TryKeyword keyword then
                if reader.IsAt TokenKind.Eq then
                    onScalar reader values
                else
                    onBlock reader values
                true
            else
                false
        | SchemaProperties _ -> false

    /// Consumes the next keyword and runs the first matching ChildKeyword member. Returns false, consuming nothing, when none matches.
    let tryDispatchChild (reader: TokenReader) (members: ContainerMember list) : bool =
        match members |> List.tryPick (function ChildKeyword(keyword, parseChild) when reader.TryKeyword keyword -> Some parseChild | _ -> None) with
        | Some parseChild ->
            parseChild reader
            true
        | None -> false

    let private parseBody
        (reader: TokenReader)
        endKind
        blockName
        (members: ContainerMember list)
        (values: Dictionary<string, string>)
        (endId: string option)
        =
        let hasSchema = members |> List.exists (function SchemaProperties _ -> true | _ -> false)
        let schema, allowExtension, allowQuotedKeys =
            match members |> List.tryPick (function SchemaProperties(s, a, q) -> Some(s, a, q) | _ -> None) with
            | Some(s, a, q) -> s, a, q
            | None -> ([] : PropertySchemas.PropertySpec list), false, false

        let specs = schema |> List.map (fun s -> s.Name, s) |> dict

        while not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader endKind endId then ()
            else
                let mutable handled = false
                for memberDef in members do
                    if not handled && tryParseMember reader memberDef values then
                        handled <- true
                if not handled then
                    while not (reader.IsOnNewline()) && not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
                        let mutable innerHandled = false
                        for memberDef in members do
                            if not innerHandled && tryParseMember reader memberDef values then
                                innerHandled <- true
                        if not innerHandled then
                            if not hasSchema then raise (reader.Unexpected())
                            MemberGrammar.readPropertyEntry
                                reader
                                specs
                                allowExtension
                                allowQuotedKeys
                                blockName
                                values

                    reader.SkipNewlines()

    let parseKeywordContainer
        (reader: TokenReader)
        endKind
        blockName
        (members: ContainerMember list)
        (endId: string option)
        =
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        parseBody reader endKind blockName members values endId
        BlockSyntax.expectBlockEnd reader endKind endId
        values

    let parseBracketContainer (reader: TokenReader) blockName (members: ContainerMember list) =
        let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        reader.SkipNewlines()
        if not (reader.IsAt TokenKind.LBrace) then
            raise (DashSpecParseException($"{blockName} requires '{{' block."))
        reader.Advance()
        reader.PushBlockClose BlockCloseStyle.Brace
        reader.SkipNewlines()
        parseBody reader "}" blockName members values None
        BlockSyntax.expectBlockEnd reader "}" None
        values
