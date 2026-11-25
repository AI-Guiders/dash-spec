namespace DashSpec.Modeling.Parse.Layout

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

/// <summary>F# AST-first SSOT for <c>module layout &lt;id&gt;</c> units (ADR-0048 / ADR-0130).</summary>
module LayoutModuleParser =

    let private mapScope (scope: LayoutScopeSyntax) =
        match scope with
        | LayoutScopeSyntax.Toolbar -> LayoutScope.Toolbar
        | LayoutScopeSyntax.Section -> LayoutScope.Tab
        | LayoutScopeSyntax.Page -> LayoutScope.Page
        | LayoutScopeSyntax.Card -> LayoutScope.Card
        | LayoutScopeSyntax.Host -> LayoutScope.Host

    let private unwrapQuotes (v: string) =
        let trimmed = v.Trim()
        if trimmed.Length >= 2 && trimmed.[0] = '"' && trimmed.[trimmed.Length - 1] = '"' then
            trimmed.Substring(1, trimmed.Length - 2)
        else
            trimmed

    let private extractHeaderWeight (tokens: SyntaxToken[]) =
        let mutable idx = 0
        let mutable found: string option = None
        while idx + 1 < tokens.Length do
            if tokens.[idx].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
               && tokens.[idx + 1].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                found <- Some tokens.[idx + 1].Text
                idx <- tokens.Length
            else
                idx <- idx + 1
        found

    let private parseCellsFromTokens (tokens: SyntaxToken[]) =
        let cells = ResizeArray<string>()
        let mutable i = 0
        while i < tokens.Length do
            match tokens.[i].LexKind with
            | DashSpec.Modeling.Parse.Lexing.TokenKind.EmptyCell ->
                if i + 2 < tokens.Length
                   && tokens.[i + 1].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
                   && tokens.[i + 2].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                    cells.Add($"[]:{tokens.[i + 2].Text}")
                    i <- i + 3
                else
                    cells.Add("[]")
                    i <- i + 1
            | DashSpec.Modeling.Parse.Lexing.TokenKind.At
                when i + 1 < tokens.Length && tokens.[i + 1].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident ->
                let name = $"@{tokens.[i + 1].Text}"
                if i + 3 < tokens.Length
                   && tokens.[i + 2].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
                   && tokens.[i + 3].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                    cells.Add($"{name}:{tokens.[i + 3].Text}")
                    i <- i + 4
                else
                    cells.Add(name)
                    i <- i + 2
            | DashSpec.Modeling.Parse.Lexing.TokenKind.Ident ->
                let name = tokens.[i].Text
                if i + 2 < tokens.Length
                   && tokens.[i + 1].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
                   && tokens.[i + 2].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                    cells.Add($"{name}:{tokens.[i + 2].Text}")
                    i <- i + 3
                else
                    cells.Add(name)
                    i <- i + 1
            | DashSpec.Modeling.Parse.Lexing.TokenKind.Comma ->
                i <- i + 1
            | _ ->
                i <- i + 1
        List.ofSeq cells

    let private isValidBareLayoutTokens (tokens: SyntaxToken[]) =
        tokens.Length > 0
        && tokens
           |> Array.forall (fun t ->
               match t.LexKind with
               | DashSpec.Modeling.Parse.Lexing.TokenKind.Ident
               | DashSpec.Modeling.Parse.Lexing.TokenKind.At
               | DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
               | DashSpec.Modeling.Parse.Lexing.TokenKind.EmptyCell
               | DashSpec.Modeling.Parse.Lexing.TokenKind.Comma
               | DashSpec.Modeling.Parse.Lexing.TokenKind.LBracket
               | DashSpec.Modeling.Parse.Lexing.TokenKind.RBracket -> true
               | _ -> false)

    let internal projectTableRowsToLayoutRows (rawRows: string[][]) : IReadOnlyList<string> list =
        let dataRows =
            rawRows
            |> Array.filter (fun cells -> cells.Length > 0 && not (TableLiteral.isSeparator cells))

        if dataRows.Length = 0 then
            []
        else
            let row0 = dataRows.[0]
            let isSlotWeightHeader =
                dataRows.Length >= 2
                && row0.Length = 2
                && (match row0.[0].Trim().ToLowerInvariant() with "slot" | "id" | "cell" -> true | _ -> false)
                && (match row0.[1].Trim().ToLowerInvariant() with "weight" | "span" -> true | _ -> false)

            if isSlotWeightHeader then
                [ for r in dataRows.[1..] do
                    if r.Length >= 2 then
                        let s = r.[0].Trim()
                        let w = r.[1].Trim()
                        if not (String.IsNullOrWhiteSpace s) && s <> "-" then
                            if String.IsNullOrWhiteSpace w || w = "-" then
                                yield [| s |] :> IReadOnlyList<string>
                            else
                                yield [| $"{s}:{w}" |] :> IReadOnlyList<string> ]
            else
                [ for r in dataRows do
                    let cells = ResizeArray<string>()
                    for rawCell in r do
                        let c = rawCell.Trim()
                        if c = "-" || c = "[]" then
                            cells.Add("[]")
                        elif not (String.IsNullOrWhiteSpace c) then
                            for part in c.Split([| ' '; ',' |], StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) do
                                cells.Add(part)
                    if cells.Count > 0 then
                        yield cells.ToArray() :> IReadOnlyList<string> ]

    let rec private projectStackRowsFromAst (isVertical: bool) (members: DashSpecAstNode[]) (synthesizedNests: ResizeArray<LayoutBoardEntry>) (nestCounter: int ref) : IReadOnlyList<string> list =
        let rows = ResizeArray<IReadOnlyList<string>>()
        let hCells = ResizeArray<string>()

        let flushHCells () =
            if not isVertical && hCells.Count > 0 then
                rows.Add(hCells.ToArray() :> IReadOnlyList<string>)
                hCells.Clear()

        let addCell (cell: string) =
            if isVertical then
                rows.Add([| cell |] :> IReadOnlyList<string>)
            else
                hCells.Add(cell)

        for memberNode in members do
            match memberNode with
            | DashSpecAstNode.GridRowDeclaration row ->
                match row.Kind with
                | GridRowKind.RulerRow _ ->
                    raise (DashSpecParseException("Bare [] grid cells belong inside 'grid … end grid'; stack rows require items or '[ … ]'."))
                | GridRowKind.BoardRow _ ->
                    if isVertical then
                        rows.Add(row.Cells :> IReadOnlyList<string>)
                    else
                        for c in row.Cells do hCells.Add(c)
            | DashSpecAstNode.BlockDeclaration childBlock ->
                match childBlock.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.HStack
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.HStack, _) ->
                    let innerRows = projectStackRowsFromAst false childBlock.Members synthesizedNests nestCounter
                    if isVertical then
                        for r in innerRows do rows.Add(r)
                    else
                        for r in innerRows do
                            for c in r do hCells.Add(c)
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.VStack
                | DashSpecBlockOpener.Named(DashSpecBlockKeyword.VStack, _) ->
                    let innerRows = projectStackRowsFromAst true childBlock.Members synthesizedNests nestCounter
                    if isVertical then
                        for r in innerRows do rows.Add(r)
                    else
                        nestCounter.Value <- nestCounter.Value + 1
                        let nestId = $"__stack_{nestCounter.Value}"
                        synthesizedNests.Add(NestRow { Id = nestId; Rows = innerRows :> IReadOnlyList<IReadOnlyList<string>> })
                        match extractHeaderWeight childBlock.Tokens with
                        | Some w -> hCells.Add($"{nestId}:{w}")
                        | None -> hCells.Add(nestId)
                | _ ->
                    let headerText = childBlock.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' inside stack."))
            | DashSpecAstNode.TableRowDeclaration row ->
                for r in projectTableRowsToLayoutRows [| row.Cells |] do
                    if isVertical then rows.Add(r)
                    else for c in r do hCells.Add(c)
            | DashSpecAstNode.PropertyAssignment p ->
                match p.Key.Trim().ToLowerInvariant() with
                | "row" | "hstack" ->
                    let cells = parseCellsFromTokens p.ValueTokens
                    if isVertical then rows.Add(cells :> IReadOnlyList<string>)
                    else for c in cells do hCells.Add(c)
                | "vstack" | "col" ->
                    for c in parseCellsFromTokens p.ValueTokens do
                        addCell c
                | _ ->
                    raise (DashSpecParseException($"Unexpected property '{p.Key}' inside stack."))
            | DashSpecAstNode.CardReference r ->
                addCell r.CardId
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | c when DashSpecAst.isTrivia c -> ()
            | other ->
                let toks = DashSpecAst.tokens other
                if toks.Length >= 3
                   && toks.[1].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
                   && (toks.[0].Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.HStack)
                       || toks.[0].Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.VStack)) then
                    let isColonVert = toks.[0].Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.VStack)
                    let cells = parseCellsFromTokens toks.[2..]
                    if isColonVert then
                        for c in cells do addCell c
                    elif isVertical then
                        rows.Add(cells :> IReadOnlyList<string>)
                    else
                        for c in cells do hCells.Add(c)
                else
                    let parsedCells = parseCellsFromTokens toks
                    if parsedCells.IsEmpty then
                        let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                        raise (DashSpecParseException($"Unexpected statement '{lineText}' inside stack."))
                    for c in parsedCells do addCell c

        flushHCells ()
        if rows.Count = 0 then
            let kw = if isVertical then "vstack" else "hstack"
            raise (DashSpecParseException($"Layout {kw} requires at least one item."))
        List.ofSeq rows

    let rec private projectBoardEntriesFromAstWithNests (members: DashSpecAstNode[]) (nestCounter: int ref) : LayoutBoardEntry list =
        let entries = ResizeArray<LayoutBoardEntry>()
        let synthesizedNests = ResizeArray<LayoutBoardEntry>()

        let tableBuffer = ResizeArray<string[]>()
        let flushTableBuffer () =
            if tableBuffer.Count > 0 then
                for r in projectTableRowsToLayoutRows (tableBuffer.ToArray()) do
                    entries.Add(CardRow r)
                tableBuffer.Clear()

        for memberNode in members do
            match memberNode with
            | DashSpecAstNode.TableRowDeclaration row ->
                tableBuffer.Add(row.Cells)
            | _ ->
                flushTableBuffer ()
                match memberNode with
                | DashSpecAstNode.GridRowDeclaration row ->
                    match row.Kind with
                    | GridRowKind.RulerRow _ ->
                        raise (DashSpecParseException("Bare [] grid cells belong inside 'grid … end grid'; board rows require '[ … ]'."))
                    | GridRowKind.BoardRow _ ->
                        entries.Add(CardRow(row.Cells :> IReadOnlyList<string>))
                | DashSpecAstNode.BlockDeclaration block ->
                    match block.Opener with
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.VStack
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.VStack, _) ->
                        let stackRows = projectStackRowsFromAst true block.Members synthesizedNests nestCounter
                        for r in stackRows do entries.Add(CardRow r)
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.HStack
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.HStack, _) ->
                        let stackRows = projectStackRowsFromAst false block.Members synthesizedNests nestCounter
                        for r in stackRows do entries.Add(CardRow r)
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Group, groupId) ->
                        let mutable title: string option = None
                        let childMembers =
                            block.Members
                            |> Array.filter (function
                                | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "title", StringComparison.OrdinalIgnoreCase) ->
                                    if p.ValueTokens.Length <> 1 || p.ValueTokens.[0].LexKind <> DashSpec.Modeling.Parse.Lexing.TokenKind.String then
                                        raise (DashSpecInvalidValueException($"Property 'title' in layout group '{groupId}' requires a string literal.", "title", p.Span.Start))
                                    title <- Some(unwrapQuotes p.ValueText)
                                    false
                                | DashSpecAstNode.PropertyAssignment p when
                                    not (String.Equals(p.Key, "row", StringComparison.OrdinalIgnoreCase)
                                         || String.Equals(p.Key, "hstack", StringComparison.OrdinalIgnoreCase)
                                         || String.Equals(p.Key, "vstack", StringComparison.OrdinalIgnoreCase)
                                         || String.Equals(p.Key, "col", StringComparison.OrdinalIgnoreCase)) ->
                                    raise (DashSpecUnknownPropertyException(p.Key, $"Unknown property '{p.Key}' in layout group '{groupId}' block.", p.Span.Start))
                                | _ -> true)

                        let innerEntries = projectBoardEntriesFromAstWithNests childMembers nestCounter
                        let rows = ResizeArray<IReadOnlyList<string>>()
                        for e in innerEntries do
                            match e with
                            | CardRow r -> rows.Add r
                            | NestRow _ -> synthesizedNests.Add e
                            | GroupRow _ -> ()

                        if rows.Count = 0 then
                            raise (DashSpecParseException($"Layout group '{groupId}' requires at least one row [ … ]."))

                        entries.Add(
                            GroupRow
                                { Id = groupId
                                  Title = title
                                  Rows = rows :> IReadOnlyList<IReadOnlyList<string>> })
                    | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Nest, nestId) ->
                        for m in block.Members do
                            match m with
                            | DashSpecAstNode.PropertyAssignment p when
                                not (String.Equals(p.Key, "row", StringComparison.OrdinalIgnoreCase)
                                     || String.Equals(p.Key, "hstack", StringComparison.OrdinalIgnoreCase)
                                     || String.Equals(p.Key, "vstack", StringComparison.OrdinalIgnoreCase)
                                     || String.Equals(p.Key, "col", StringComparison.OrdinalIgnoreCase)) ->
                                raise (DashSpecUnknownPropertyException(p.Key, $"Unknown property '{p.Key}' in layout nest '{nestId}' block.", p.Span.Start))
                            | _ -> ()

                        let innerEntries = projectBoardEntriesFromAstWithNests block.Members nestCounter
                        let rows = ResizeArray<IReadOnlyList<string>>()
                        for e in innerEntries do
                            match e with
                            | CardRow r -> rows.Add r
                            | NestRow _ -> synthesizedNests.Add e
                            | GroupRow _ -> ()

                        if rows.Count = 0 then
                            raise (DashSpecParseException($"Layout nest '{nestId}' requires at least one row [ … ]."))

                        entries.Add(NestRow { Id = nestId; Rows = rows :> IReadOnlyList<IReadOnlyList<string>> })
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Group ->
                        raise (DashSpecParseException("Layout group requires an id."))
                    | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Nest ->
                        raise (DashSpecParseException("Layout nest requires an id."))
                    | _ ->
                        let headerText = block.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                        raise (DashSpecParseException($"Unexpected block '{headerText}' in layout module."))
                | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "scope", StringComparison.OrdinalIgnoreCase) -> ()
                | DashSpecAstNode.PropertyAssignment p ->
                    match p.Key.Trim().ToLowerInvariant() with
                    | "row" | "hstack" ->
                        let cells = parseCellsFromTokens p.ValueTokens
                        if not cells.IsEmpty then
                            entries.Add(CardRow(cells :> IReadOnlyList<string>))
                    | "vstack" | "col" ->
                        for c in parseCellsFromTokens p.ValueTokens do
                            entries.Add(CardRow([| c |] :> IReadOnlyList<string>))
                    | _ ->
                        raise (DashSpecParseException($"Unknown property '{p.Key}' in layout module."))
                | DashSpecAstNode.ClauseDeclaration c ->
                    match c.Kind with
                    | ClauseKind.ScopeClause _ -> ()
                    | _ ->
                        let toks = c.Tokens
                        if isValidBareLayoutTokens toks then
                            let cells = parseCellsFromTokens toks
                            if not cells.IsEmpty then
                                entries.Add(CardRow(cells :> IReadOnlyList<string>))
                        else
                            let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                            raise (DashSpecParseException($"Unexpected clause '{lineText}' in layout module."))
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _ -> ()
                | c when DashSpecAst.isTrivia c -> ()
                | other ->
                    let toks = DashSpecAst.tokens other
                    if toks.Length >= 2 && toks.[0].Keyword = Some DashSpecKeyword.Scope then
                        raise (DashSpecParseException($"Layout module scope must be toolbar, tab, page, card, or host; got '{toks.[1].Text}'."))
                    elif toks.Length >= 3
                         && toks.[1].LexKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
                         && (toks.[0].Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.HStack)
                             || toks.[0].Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.VStack)) then
                        let isVert = toks.[0].Keyword = Some(DashSpecKeyword.Block DashSpecBlockKeyword.VStack)
                        let cells = parseCellsFromTokens toks.[2..]
                        if isVert then
                            for c in cells do
                                entries.Add(CardRow([| c |] :> IReadOnlyList<string>))
                        elif not cells.IsEmpty then
                            entries.Add(CardRow(cells :> IReadOnlyList<string>))
                    elif isValidBareLayoutTokens toks then
                        let cells = parseCellsFromTokens toks
                        if not cells.IsEmpty then
                            entries.Add(CardRow(cells :> IReadOnlyList<string>))
                        else
                            let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                            raise (DashSpecParseException($"Unexpected statement '{lineText}' in layout module."))
                    else
                        let lineText = toks |> Array.map (fun t -> t.Text) |> String.concat " "
                        raise (DashSpecParseException($"Unexpected statement '{lineText}' in layout module."))

        flushTableBuffer ()
        (List.ofSeq synthesizedNests) @ (List.ofSeq entries)

    let private projectBoardEntriesFromAst (members: DashSpecAstNode[]) : LayoutBoardEntry list =
        let counter = ref 0
        projectBoardEntriesFromAstWithNests members counter

    /// Expand `@filters` and `@primary` role placeholders on a card interior `LayoutBoardDefinition`.
    let expandCardRoleTokens
        (localFilters: IReadOnlyList<string>)
        (primaryToken: string)
        (board: LayoutBoardDefinition)
        : LayoutBoardDefinition =
        let expandCell (cell: string) : string list =
            let trimmed = cell.Trim()
            let name, weightSuffix =
                let colonIdx = trimmed.IndexOf(':')
                if colonIdx > 0 then
                    trimmed.Substring(0, colonIdx), trimmed.Substring(colonIdx)
                else
                    trimmed, ""

            let norm = name.TrimStart('@')
            if String.Equals(norm, "primary", StringComparison.OrdinalIgnoreCase) then
                [ primaryToken + weightSuffix ]
            elif String.Equals(norm, "filters", StringComparison.OrdinalIgnoreCase) then
                if isNull localFilters || localFilters.Count = 0 then
                    []
                else
                    [ for f in localFilters -> f + weightSuffix ]
            else
                [ trimmed ]

        let expandRow (row: IReadOnlyList<string>) : IReadOnlyList<string> option =
            let expanded =
                [ for c in row do
                    yield! expandCell c ]
            if expanded.IsEmpty then None
            else Some(expanded :> IReadOnlyList<string>)

        let expandEntries (entries: IReadOnlyList<LayoutBoardEntry>) : IReadOnlyList<LayoutBoardEntry> =
            [ for entry in entries do
                match entry with
                | CardRow row ->
                    match expandRow row with
                    | Some r -> yield CardRow r
                    | None -> ()
                | GroupRow g ->
                    let rows = g.Rows |> Seq.choose expandRow |> Seq.toList
                    if not rows.IsEmpty then
                        yield GroupRow { g with Rows = rows :> IReadOnlyList<_> }
                | NestRow n ->
                    let rows = n.Rows |> Seq.choose expandRow |> Seq.toList
                    if not rows.IsEmpty then
                        yield NestRow { n with Rows = rows :> IReadOnlyList<_> } ]
            :> IReadOnlyList<LayoutBoardEntry>

        { board with
            Entries = expandEntries board.Entries
            MidEntries = board.MidEntries |> Option.map expandEntries
            NarrowEntries = board.NarrowEntries |> Option.map expandEntries }

    /// Project a `grid … end grid` AST block into `LayoutDefinition`.
    let projectGridFromAst (members: DashSpecAstNode[]) : LayoutDefinition =
        let rowLengths = ResizeArray<int>()
        for child in members do
            match child with
            | DashSpecAstNode.GridRowDeclaration { Kind = GridRowKind.RulerRow cols } ->
                rowLengths.Add cols
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | c when DashSpecAst.isTrivia c -> ()
            | _ ->
                raise (DashSpecParseException("Layout grid requires the drawing: draw it with [] cells — columns = N is dead."))

        if rowLengths.Count = 0 then
            raise (DashSpecParseException("Layout grid requires the drawing: draw it with [] cells — columns = N is dead."))

        { Columns = rowLengths |> Seq.max }

    /// <summary>Project a <c>module layout &lt;id&gt;</c> envelope directly from the typed syntax tree.</summary>
    let parseEnvelope (env: UnitGrammar.UnitEnvelope) : LayoutBoardDefinition =
        let scope =
            env.Body
            |> Array.tryPick (function
                | DashSpecAstNode.ClauseDeclaration c ->
                    match c.Kind with
                    | ClauseKind.ScopeClause s -> Some(mapScope s)
                    | _ -> None
                | DashSpecAstNode.PropertyAssignment p when String.Equals(p.Key, "scope", StringComparison.OrdinalIgnoreCase) ->
                    match unwrapQuotes(p.ValueText).Trim().ToLowerInvariant() with
                    | "toolbar" -> Some LayoutScope.Toolbar
                    | "section" -> Some LayoutScope.Tab
                    | "page" -> Some LayoutScope.Page
                    | "card" -> Some LayoutScope.Card
                    | "host" -> Some LayoutScope.Host
                    | bad -> raise (DashSpecParseException($"Layout module scope must be toolbar, tab, page, card, or host; got '{bad}'."))
                | DashSpecAstNode.Line l when l.Tokens.Length >= 2 && l.Tokens.[0].Keyword = Some DashSpecKeyword.Scope ->
                    raise (DashSpecParseException($"Layout module scope must be toolbar, tab, page, card, or host; got '{l.Tokens.[1].Text}'."))
                | _ -> None)
            |> Option.defaultWith (fun () ->
                raise (DashSpecParseException("Layout module requires scope toolbar|tab|page|card|host after @layout <id>.")))

        let gridBlocks =
            env.Body
            |> Array.choose (function
                | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid -> Some b
                | _ -> None)

        if gridBlocks.Length > 1 then
            raise (DashSpecParseException("Layout module allows a single grid block."))

        let gridColumns =
            match gridBlocks with
            | [||] -> 0
            | [| gridBlock |] -> (projectGridFromAst gridBlock.Members).Columns
            | _ -> 0

        let nonGridMembers =
            env.Body
            |> Array.filter (function
                | DashSpecAstNode.BlockDeclaration b -> b.Opener <> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid
                | _ -> true)

        let tryViewportBlock kw =
            nonGridMembers
            |> Array.tryPick (function
                | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous kw ->
                    let innerEntries = projectBoardEntriesFromAstWithNests b.Members (ref 0)
                    if innerEntries.IsEmpty then
                        let kwName = DashSpecBlockKeyword.toEndName kw
                        raise (DashSpecParseException($"Layout {kwName} block requires at least one row or stack."))
                    Some b
                | _ -> None)

        let wideBlock = tryViewportBlock DashSpecBlockKeyword.Wide
        let midBlock = tryViewportBlock DashSpecBlockKeyword.Mid
        let narrowBlock = tryViewportBlock DashSpecBlockKeyword.Narrow

        let directBoardMembers =
            nonGridMembers
            |> Array.filter (function
                | DashSpecAstNode.BlockDeclaration b when
                    b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Wide
                    || b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Mid
                    || b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Narrow -> false
                | _ -> true)

        let counter = ref 0
        let entries =
            match wideBlock with
            | Some wb -> projectBoardEntriesFromAstWithNests wb.Members counter
            | None -> projectBoardEntriesFromAstWithNests directBoardMembers counter

        let midEntries =
            midBlock
            |> Option.map (fun mb -> projectBoardEntriesFromAstWithNests mb.Members counter :> IReadOnlyList<LayoutBoardEntry>)

        let narrowEntries =
            narrowBlock
            |> Option.map (fun nb -> projectBoardEntriesFromAstWithNests nb.Members counter :> IReadOnlyList<LayoutBoardEntry>)

        if entries.IsEmpty && gridColumns = 0 then
            raise (DashSpecParseException("Layout board requires at least one row [ … ], group { … }, or nest { … }."))

        { Entries = entries :> IReadOnlyList<_>
          ModuleScope = Some scope
          GridColumns = gridColumns
          MidEntries = midEntries
          NarrowEntries = narrowEntries }

    /// Project a layout board (`LayoutBoardDefinition`) from an array of AST nodes (Tier 3 MatrixConstruct entry point).
    let projectBoardFromAst (members: DashSpecAstNode[]) : LayoutBoardDefinition =
        let tryViewportBlock kw =
            members
            |> Array.tryPick (function
                | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous kw -> Some b
                | _ -> None)

        let wideBlock = tryViewportBlock DashSpecBlockKeyword.Wide
        let midBlock = tryViewportBlock DashSpecBlockKeyword.Mid
        let narrowBlock = tryViewportBlock DashSpecBlockKeyword.Narrow

        let directBoardMembers =
            members
            |> Array.filter (function
                | DashSpecAstNode.BlockDeclaration b when
                    b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Wide
                    || b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Mid
                    || b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Narrow -> false
                | _ -> true)

        let counter = ref 0
        let projectViewport (kw: DashSpecBlockKeyword) (b: BlockDeclarationSyntax) =
            let res = projectBoardEntriesFromAstWithNests b.Members counter
            if res.IsEmpty then
                raise (DashSpecParseException($"Layout {DashSpecBlockKeyword.toEndName kw} block requires at least one row or stack."))
            res

        let entries =
            match wideBlock with
            | Some wb ->
                let wEntries = projectViewport DashSpecBlockKeyword.Wide wb
                let dEntries = projectBoardEntriesFromAstWithNests directBoardMembers counter
                wEntries @ dEntries
            | None -> projectBoardEntriesFromAstWithNests directBoardMembers counter

        let midEntries =
            midBlock
            |> Option.map (fun mb -> projectViewport DashSpecBlockKeyword.Mid mb :> IReadOnlyList<LayoutBoardEntry>)

        let narrowEntries =
            narrowBlock
            |> Option.map (fun nb -> projectViewport DashSpecBlockKeyword.Narrow nb :> IReadOnlyList<LayoutBoardEntry>)

        if entries.IsEmpty then
            raise (DashSpecParseException("Layout board requires at least one row [ … ], vstack, hstack, group { … }, or nest { … }."))

        { Entries = entries :> IReadOnlyList<_>
          ModuleScope = None
          GridColumns = 0
          MidEntries = midEntries
          NarrowEntries = narrowEntries }

    let private parseSpanValue (value: string) =
        match value.ToLowerInvariant() with
        | "full" -> 12
        | "half" -> 6
        | "third" -> 4
        | _ ->
            let mutable parsed = 0
            if Int32.TryParse(value, &parsed) && parsed > 0 then parsed else 6

    /// Project a `place … end place` block from AST members via Tier 1 (`PropertyBlockParser.projectPropertiesFromAst`).
    let projectPlacementFromAst (members: DashSpecAstNode[]) : PlacementDefinition =
        let props = PropertyBlockParser.projectPropertiesFromAst members PropertySchemas.placement "place" false
        let parsePos name =
            match props.TryGetValue name with
            | true, raw ->
                let mutable parsed = 0
                if Int32.TryParse(raw, &parsed) && parsed > 0 then parsed else 1
            | false, _ -> 1
        let span =
            match props.TryGetValue "span" with
            | true, raw -> parseSpanValue raw
            | false, _ -> 6
        { Row = parsePos "row"; Col = parsePos "col"; Span = span }

    let parsePlacement (reader: DashSpec.Modeling.Parse.Lexing.TokenReader) : PlacementDefinition =
        let props = PropertyBlockParser.parse reader PropertySchemas.placement "place" false false
        let parsePos name =
            match props.TryGetValue name with
            | true, raw ->
                let mutable parsed = 0
                if Int32.TryParse(raw, &parsed) && parsed > 0 then parsed else 1
            | false, _ -> 1
        let span =
            match props.TryGetValue "span" with
            | true, raw -> parseSpanValue raw
            | false, _ -> 6
        { Row = parsePos "row"; Col = parsePos "col"; Span = span }

    let defaultGrid () =
        match GridDefaultBridge.Default with
        | Some grid -> grid
        | None ->
            raise (DashSpecParseException("Grid not drawn: layout grid with [] cells is required, and no host default is registered."))

    let private readTokensUntilBoardStop
        (reader: DashSpec.Modeling.Parse.Lexing.TokenReader)
        (endKind: string option)
        (endId: string option)
        : DashSpec.Modeling.Parse.Lexing.Token list
        =
        let tokens = ResizeArray<DashSpec.Modeling.Parse.Lexing.Token>()
        let mutable braceDepth = 0
        let mutable endKeywordDepth = 0

        let isNestedBlockOpener () =
            match reader.TryPeekIdent() with
            | Some kw ->
                match kw.ToLowerInvariant() with
                | "wide" | "mid" | "narrow" | "vstack" | "hstack" | "group" | "nest" ->
                    let fork = reader.Fork()
                    fork.ReadIdent() |> ignore
                    if (kw.Equals("group", StringComparison.OrdinalIgnoreCase) || kw.Equals("nest", StringComparison.OrdinalIgnoreCase))
                       && fork.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                        fork.ReadIdentSameLine() |> ignore
                    if fork.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Colon then
                        fork.Advance()
                        if fork.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                            let w = fork.ReadIdentSameLine()
                            (Int32.TryParse w |> fst)
                            && (fork.IsOnNewline() || fork.IsAt DashSpec.Modeling.Parse.Lexing.TokenKind.LBrace)
                        else
                            false
                    elif fork.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Eq then
                        false
                    else
                        fork.IsOnNewline() || fork.IsAt DashSpec.Modeling.Parse.Lexing.TokenKind.LBrace
                | _ -> false
            | None -> false

        let isNestedBlockEnd () =
            let fork = reader.Fork()
            if fork.TryKeyword "end" && fork.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident then
                let k = fork.ReadIdentSameLine().ToLowerInvariant()
                k = "wide" || k = "mid" || k = "narrow" || k = "vstack" || k = "hstack" || k = "group" || k = "nest"
            else
                false

        let shouldStop () =
            reader.IsEof
            || (braceDepth = 0 && reader.IsAt DashSpec.Modeling.Parse.Lexing.TokenKind.RBrace)
            || (braceDepth = 0 && endKeywordDepth = 0 && endKind.IsSome && BlockSyntax.isBlockEnd reader endKind.Value endId)
            || (braceDepth = 0 && endKeywordDepth = 0 && endKind = Some "layout" && reader.TryPeekIdent() = Some "place")

        while not (shouldStop ()) do
            if isNestedBlockOpener () then
                let kw = reader.ReadIdent()
                tokens.Add({ Kind = DashSpec.Modeling.Parse.Lexing.TokenKind.Ident; Value = kw; Start = 0; Length = kw.Length })
                while not (reader.IsOnNewline()) && reader.RawKind <> DashSpec.Modeling.Parse.Lexing.TokenKind.Eof do
                    let cur = reader.Current
                    if cur.Kind = DashSpec.Modeling.Parse.Lexing.TokenKind.LBrace then
                        braceDepth <- braceDepth + 1
                    elif cur.Kind = DashSpec.Modeling.Parse.Lexing.TokenKind.RBrace && braceDepth > 0 then
                        braceDepth <- braceDepth - 1
                    tokens.Add cur
                    reader.Advance()
                if braceDepth = 0 then
                    endKeywordDepth <- endKeywordDepth + 1
            elif isNestedBlockEnd () then
                if endKeywordDepth > 0 then
                    endKeywordDepth <- endKeywordDepth - 1
                while not (reader.IsOnNewline()) && reader.RawKind <> DashSpec.Modeling.Parse.Lexing.TokenKind.Eof do
                    tokens.Add reader.Current
                    reader.Advance()
            else
                let cur = reader.Current
                if cur.Kind = DashSpec.Modeling.Parse.Lexing.TokenKind.LBrace then
                    braceDepth <- braceDepth + 1
                elif cur.Kind = DashSpec.Modeling.Parse.Lexing.TokenKind.RBrace && braceDepth > 0 then
                    braceDepth <- braceDepth - 1
                tokens.Add cur
                reader.Advance()

            while reader.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.Newline
                  || reader.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.LineComment
                  || reader.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.DocComment
                  || reader.RawKind = DashSpec.Modeling.Parse.Lexing.TokenKind.BlockComment do
                tokens.Add reader.Current
                reader.Advance()

        List.ofSeq tokens

    let private renderTokensToText (tokens: DashSpec.Modeling.Parse.Lexing.Token list) : string =
        let sb = System.Text.StringBuilder()
        let mutable prevKind: DashSpec.Modeling.Parse.Lexing.TokenKind option = None
        for t in tokens do
            match t.Kind with
            | DashSpec.Modeling.Parse.Lexing.TokenKind.Newline ->
                sb.Append('\n') |> ignore
                prevKind <- Some t.Kind
            | DashSpec.Modeling.Parse.Lexing.TokenKind.LineComment
            | DashSpec.Modeling.Parse.Lexing.TokenKind.DocComment
            | DashSpec.Modeling.Parse.Lexing.TokenKind.BlockComment -> ()
            | DashSpec.Modeling.Parse.Lexing.TokenKind.String ->
                if prevKind.IsSome && prevKind <> Some DashSpec.Modeling.Parse.Lexing.TokenKind.Newline then
                    sb.Append(' ') |> ignore
                sb.Append('"').Append(t.Value).Append('"') |> ignore
                prevKind <- Some t.Kind
            | DashSpec.Modeling.Parse.Lexing.TokenKind.Colon ->
                if prevKind = Some DashSpec.Modeling.Parse.Lexing.TokenKind.Ident
                   || prevKind = Some DashSpec.Modeling.Parse.Lexing.TokenKind.EmptyCell then
                    sb.Append(':') |> ignore
                else
                    sb.Append(" :") |> ignore
                prevKind <- Some t.Kind
            | DashSpec.Modeling.Parse.Lexing.TokenKind.At ->
                if prevKind.IsSome && prevKind <> Some DashSpec.Modeling.Parse.Lexing.TokenKind.Newline then
                    sb.Append(' ') |> ignore
                sb.Append('@') |> ignore
                prevKind <- Some t.Kind
            | _ ->
                if prevKind = Some DashSpec.Modeling.Parse.Lexing.TokenKind.Colon
                   || prevKind = Some DashSpec.Modeling.Parse.Lexing.TokenKind.At
                   || prevKind = Some DashSpec.Modeling.Parse.Lexing.TokenKind.Dot then
                    sb.Append(t.Value) |> ignore
                else
                    if prevKind.IsSome && prevKind <> Some DashSpec.Modeling.Parse.Lexing.TokenKind.Newline then
                        sb.Append(' ') |> ignore
                    sb.Append(t.Value) |> ignore
                prevKind <- Some t.Kind
        sb.ToString()

    /// Parse a layout board text slice via the AST-first Tier 3 MatrixConstruct projection.
    let parseBoardText (bodyText: string) : LayoutBoardDefinition =
        let wrapped = $"layout\n{bodyText}\nend layout"
        let tree = DashSpecSurfaceSyntax.parse wrapped
        let members =
            match tree.Root with
            | DashSpecAstNode.CompilationUnit cu ->
                cu.Members
                |> Array.tryPick (function
                    | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Layout ->
                        Some b.Members
                    | _ -> None)
                |> Option.defaultValue cu.Members
            | other -> DashSpecAst.members other
        projectBoardFromAst members

    /// Parse board rows from a TokenReader by projecting through the Tier 3 AST normalizer (`parseBoardText`).
    let parseBoardRows (reader: DashSpec.Modeling.Parse.Lexing.TokenReader) (endKind: string option) (endId: string option) : LayoutBoardDefinition =
        reader.SkipNewlines()
        let bodyTokens = readTokensUntilBoardStop reader endKind endId
        let bodyText = renderTokensToText bodyTokens
        parseBoardText bodyText

    let parseBoard (reader: DashSpec.Modeling.Parse.Lexing.TokenReader) : LayoutBoardDefinition =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let board = parseBoardRows reader (Some "layout") None
        BlockSyntax.expectBlockEnd reader "layout" None
        board

    let parseGridBody (reader: DashSpec.Modeling.Parse.Lexing.TokenReader) : LayoutDefinition =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let bodyTokens = readTokensUntilBoardStop reader (Some "grid") None
        BlockSyntax.expectBlockEnd reader "grid" None
        let wrapped = $"grid\n{renderTokensToText bodyTokens}\nend grid"
        let tree = DashSpecSurfaceSyntax.parse wrapped
        let members =
            match tree.Root with
            | DashSpecAstNode.CompilationUnit cu ->
                cu.Members
                |> Array.tryPick (function
                    | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid ->
                        Some b.Members
                    | _ -> None)
                |> Option.defaultValue cu.Members
            | other -> DashSpecAst.members other
        projectGridFromAst members

    let parseGrid (reader: DashSpec.Modeling.Parse.Lexing.TokenReader) : LayoutDefinition =
        match reader.ReadIdent() with
        | "grid" -> ()
        | _ -> raise (reader.Unexpected("grid"))
        parseGridBody reader

    let parseLayoutFile (text: string) : LayoutBoardDefinition =
        UnitGrammar.parseUnit DashSpecModuleKind.Layout parseEnvelope text
