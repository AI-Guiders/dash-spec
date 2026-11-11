namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Formatting
open DashSpec.Modeling.Parse.Lexing
open AIGuiders.Platform.Modeling.Core.Identity

module DashSpecBlockOpenerSyntax =

    let ofLineKind (lineKind: BlockSurfaceLineClassifier.LineKind) =
        match lineKind with
        | BlockSurfaceLineClassifier.BlockOpener(kind, id) ->
            match DashSpecBlockKeyword.tryOfName kind, id with
            | Some keyword, Some identifier -> DashSpecBlockOpener.Named(keyword, identifier)
            | Some keyword, None -> DashSpecBlockOpener.Anonymous keyword
            | None, Some identifier -> DashSpecBlockOpener.Named(DashSpecBlockKeyword.Other kind, identifier)
            | None, None -> DashSpecBlockOpener.Anonymous(DashSpecBlockKeyword.Other kind)
        | _ -> failwith "not a block opener line"

/// Single SSOT entry: lex once → block-surface <see cref="ParseTree"/> (Code Center + authoring bridge).
module DashSpecSurfaceSyntax =

    [<RequireQualifiedAccess>]
    type private NodePayload =
        | CompilationUnit
        | Module of directive: DashSpecModuleDirective
        | Block of opener: DashSpecBlockOpener * typeAnnotation: string option * origin: SourceOriginSyntax option
        | CardRef of cardId: string
        | End of endKeyword: DashSpecBlockKeyword * endId: string option
        | Import of keyword: string * kind: string option * target: string * alias: string option
        | TypeField of optional: bool * fieldType: DashType * fieldName: string
        | Property of key: string * valueText: string * keyTokens: SyntaxToken[] * eqToken: SyntaxToken * valueTokens: SyntaxToken[]
        | Port of role: PortDeclRole * shape: string option * portName: string * valueType: string option * defaultValue: string option * columnBinding: string option
        | FlowLink of fromNode: string * fromPort: string option * toNode: string * toPort: string option
        | Clause of kind: ClauseKind
        | GridRow of cells: string[]
        | TableRow of cells: string[]
        | Line
        | BlankLine

    type private MutableNode =
        { Payload: NodePayload
          Id: NodeId
          Span: TextSpan
          Tokens: SyntaxToken[]
          Children: ResizeArray<MutableNode> }

    let private mkNode payload id span tokens =
        { Payload = payload
          Id = id
          Span = span
          Tokens = tokens
          Children = ResizeArray() }

    let private nextId (counter: int ref) =
        let id = NodeId.mint (NumericId.ofCounter (int64 counter.Value))
        counter.Value <- counter.Value + 1
        id

    let private tokenIndex (allTokens: Token[]) (token: Token) =
        allTokens |> Array.findIndex (fun t -> t.Start = token.Start && t.Kind = token.Kind)

    let private lineSyntaxTokens (source: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) =
        line.Tokens
        |> List.map (fun token ->
            SyntaxTokenClassifier.toSyntaxToken source allTokens (tokenIndex allTokens token) token)
        |> Array.ofList

    let private isCardsParent (frame: MutableNode) =
        match frame.Payload with
        | NodePayload.Block(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Cards, _, _)
        | NodePayload.Block(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Cards, _), _, _) -> true
        | _ -> false

    let private isTypeParent (frame: MutableNode) =
        match frame.Payload with
        | NodePayload.Block(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Type, _, _)
        | NodePayload.Block(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, _), _, _) -> true
        | _ -> false

    let private isPortsParent (frame: MutableNode) =
        match frame.Payload with
        | NodePayload.Block(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports, _, _)
        | NodePayload.Block(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _), _, _) -> true
        | _ -> false

    let rec private toImmutable (node: MutableNode) : DashSpecAstNode =
        let children = node.Children |> Seq.map toImmutable |> Array.ofSeq

        let span =
            if children.Length = 0 then
                node.Span
            else
                let start =
                    children
                    |> Array.map (fun child -> (DashSpecAst.span child).Start)
                    |> Array.append [| node.Span.Start |]
                    |> Array.min

                let stop =
                    children
                    |> Array.map (fun child -> (DashSpecAst.span child).End)
                    |> Array.append [| node.Span.End |]
                    |> Array.max

                TextSpan.Create start (stop - start)

        match node.Payload with
        | NodePayload.CompilationUnit ->
            DashSpecAstNode.CompilationUnit { Id = node.Id; Span = span; Members = children }
        | NodePayload.Module directive ->
            DashSpecAstNode.ModuleDeclaration
                { Id = node.Id
                  Span = span
                  Directive = directive
                  HeaderSpan = node.Span
                  Tokens = node.Tokens
                  Members = children }
        | NodePayload.Block(opener, typeAnnotation, origin) ->
            DashSpecAstNode.BlockDeclaration
                { Id = node.Id
                  Span = span
                  Opener = opener
                  TypeAnnotation = typeAnnotation
                  Origin = origin
                  HeaderSpan = node.Span
                  Tokens = node.Tokens
                  Members = children }
        | NodePayload.CardRef cardId ->
            DashSpecAstNode.CardReference
                { Id = node.Id
                  Span = span
                  CardId = cardId
                  Tokens = node.Tokens }
        | NodePayload.End(endKeyword, endId) ->
            DashSpecAstNode.EndBlock
                { Id = node.Id
                  Span = span
                  EndKeyword = endKeyword
                  EndId = endId
                  Tokens = node.Tokens }
        | NodePayload.Import(keyword, kind, target, aliasName) ->
            DashSpecAstNode.ImportDeclaration
                { Id = node.Id
                  Span = span
                  Keyword = keyword
                  Kind = kind
                  Target = target
                  Alias = aliasName
                  Tokens = node.Tokens }
        | NodePayload.TypeField(optional, fieldType, fieldName) ->
            DashSpecAstNode.TypeFieldDeclaration
                { Id = node.Id
                  Span = span
                  Optional = optional
                  FieldType = fieldType
                  FieldName = fieldName
                  Tokens = node.Tokens }
        | NodePayload.Property(key, valueText, keyTokens, eqToken, valueTokens) ->
            DashSpecAstNode.PropertyAssignment
                { Id = node.Id
                  Span = span
                  Key = key
                  ValueText = valueText
                  KeyTokens = keyTokens
                  EqualsToken = eqToken
                  ValueTokens = valueTokens
                  Tokens = node.Tokens }
        | NodePayload.Port(role, shape, portName, valueType, defaultValue, columnBinding) ->
            DashSpecAstNode.PortDeclaration
                { Id = node.Id
                  Span = span
                  Role = role
                  PortShape = shape
                  PortName = portName
                  ValueType = valueType
                  DefaultValue = defaultValue
                  ColumnBinding = columnBinding
                  Tokens = node.Tokens }
        | NodePayload.FlowLink(fromNode, fromPort, toNode, toPort) ->
            DashSpecAstNode.FlowLinkDeclaration
                { Id = node.Id
                  Span = span
                  FromNode = fromNode
                  FromPort = fromPort
                  ToNode = toNode
                  ToPort = toPort
                  Tokens = node.Tokens }
        | NodePayload.Clause kind ->
            DashSpecAstNode.ClauseDeclaration
                { Id = node.Id
                  Span = span
                  Kind = kind
                  Tokens = node.Tokens }
        | NodePayload.GridRow cells ->
            DashSpecAstNode.GridRowDeclaration
                { Id = node.Id
                  Span = span
                  Cells = cells
                  Tokens = node.Tokens }
        | NodePayload.TableRow cells ->
            DashSpecAstNode.TableRowDeclaration
                { Id = node.Id
                  Span = span
                  Cells = cells
                  Tokens = node.Tokens }
        | NodePayload.Line -> DashSpecAstNode.Line { Id = node.Id; Span = span; Tokens = node.Tokens }
        | NodePayload.BlankLine -> DashSpecAstNode.BlankLine { Id = node.Id; Span = span }

    let private appendBlank (parent: MutableNode) (id: int ref) (line: TokenReaderPhysicalLines.PhysicalLine) =
        parent.Children.Add(mkNode NodePayload.BlankLine (nextId id) line.Span Array.empty)

    let private appendLine (parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) =
        parent.Children.Add(
            mkNode NodePayload.Line (nextId id) line.Span (lineSyntaxTokens text allTokens line))

    /// Comment-only lines are trivia: kept as Line nodes with their comment tokens, never as blank lines (blank lines drop the comment text).
    let private appendTriviaOrBlank
        (parent: MutableNode)
        (id: int ref)
        (text: string)
        (allTokens: Token[])
        (line: TokenReaderPhysicalLines.PhysicalLine)
        =
        let isComment (token: Token) =
            token.Kind = TokenKind.LineComment || token.Kind = TokenKind.BlockComment

        if line.Tokens |> List.exists isComment then
            appendLine parent id text allTokens line
        else
            appendBlank parent id line

    let private appendEndBlock
        (parent: MutableNode)
        (id: int ref)
        (text: string)
        (allTokens: Token[])
        (line: TokenReaderPhysicalLines.PhysicalLine)
        (kind: string)
        (endId: string option)
        =
        let endKeyword =
            DashSpecBlockKeyword.tryOfName kind
            |> Option.defaultWith (fun () -> DashSpecBlockKeyword.Other kind)

        parent.Children.Add(
            mkNode (NodePayload.End(endKeyword, endId)) (nextId id) line.Span (lineSyntaxTokens text allTokens line))

    let private appendCardRef (parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (cardId: string) =
        parent.Children.Add(
            mkNode (NodePayload.CardRef cardId) (nextId id) line.Span (lineSyntaxTokens text allTokens line))

    let private appendImport
        (parent: MutableNode)
        (id: int ref)
        (text: string)
        (allTokens: Token[])
        (line: TokenReaderPhysicalLines.PhysicalLine)
        (keyword: string)
        (kind: string option)
        (target: string)
        (aliasName: string option)
        =
        parent.Children.Add(
            mkNode (NodePayload.Import(keyword, kind, target, aliasName)) (nextId id) line.Span (lineSyntaxTokens text allTokens line))

    let private significantTokens (tokens: Token list) =
        tokens
        |> List.filter (fun t ->
            t.Kind <> TokenKind.Newline
            && t.Kind <> TokenKind.Eof
            && t.Kind <> TokenKind.LineComment
            && t.Kind <> TokenKind.BlockComment)

    let private tryParseDottedName (tokens: Token list) =
        let filtered = significantTokens tokens

        if filtered.IsEmpty then
            None
        elif filtered |> List.forall (fun t -> t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot) then
            let joined = filtered |> List.map (fun t -> t.Value) |> String.concat ""
            if String.IsNullOrWhiteSpace joined || joined.StartsWith('.') || joined.EndsWith('.') then None
            else Some joined
        else
            None

    let private tryParseTypeFieldLine (tokens: Token list) : (bool * DashType * string) option =
        let sigTokens = significantTokens tokens

        let optional, afterOpt =
            match sigTokens with
            | { Kind = TokenKind.Ident; Value = kw } :: rest
                when String.Equals(kw, "optional", StringComparison.OrdinalIgnoreCase) ->
                true, rest
            | _ -> false, sigTokens

        match afterOpt with
        | { Kind = TokenKind.Ident; Value = arrKw } :: { Kind = TokenKind.Ident; Value = elemName } :: { Kind = TokenKind.Ident; Value = lenText } :: nameTokens
            when String.Equals(arrKw, "array", StringComparison.OrdinalIgnoreCase) ->
            match DashPrimitive.tryParseFieldKeyword elemName, Int32.TryParse lenText, tryParseDottedName nameTokens with
            | Some elem, (true, len), Some fieldName when len > 0 ->
                Some(optional, DashType.FixedArray(elem, len), fieldName)
            | _ -> None
        | { Kind = TokenKind.Ident; Value = typeName } :: nameTokens when not nameTokens.IsEmpty ->
            match tryParseDottedName nameTokens with
            | Some fieldName ->
                let dashType =
                    match DashPrimitive.tryParseFieldKeyword typeName with
                    | Some prim -> DashType.Primitive prim
                    | None -> DashType.Named typeName
                Some(optional, dashType, fieldName)
            | None -> None
        | _ -> None

    /// Strip trailing `}` on a single-line brace-closed property line (e.g. `transform use to_zone { zone = UTC+3 }`).
    let private stripTrailingBraces (syntaxTokens: SyntaxToken[]) =
        let mutable endIdx = syntaxTokens.Length - 1
        while endIdx >= 0 && syntaxTokens.[endIdx].LexKind = TokenKind.RBrace do
            endIdx <- endIdx - 1
        if endIdx < 0 then [||]
        else syntaxTokens.[0 .. endIdx]

    let private tryParsePropertyLine (syntaxTokens: SyntaxToken[]) =
        let nonTrivia =
            syntaxTokens
            |> Array.filter (fun t ->
                t.LexKind <> TokenKind.LineComment
                && t.LexKind <> TokenKind.BlockComment)
            |> stripTrailingBraces

        match nonTrivia |> Array.tryFindIndex (fun t -> t.LexKind = TokenKind.Eq) with
        | Some eqIdx when eqIdx > 0 && eqIdx < nonTrivia.Length - 1 ->
            let keyTokens = nonTrivia.[0 .. eqIdx - 1]
            let eqToken = nonTrivia.[eqIdx]
            let valueTokens = nonTrivia.[eqIdx + 1 ..]

            if keyTokens |> Array.forall (fun t -> t.LexKind = TokenKind.Ident || t.LexKind = TokenKind.Dot) then
                // ADR-0130: one property per line — a second `=` on the line is
                // a multi-assignment, which stays an untyped Line for diagnostics.
                let valueHasAssignment =
                    valueTokens
                    |> Array.exists (fun t -> t.LexKind = TokenKind.Eq)

                if valueHasAssignment then
                    None
                else
                    let key = keyTokens |> Array.map (fun t -> t.Text) |> String.concat ""
                    let valueText =
                        valueTokens
                        |> Array.map (fun t -> t.Text)
                        |> String.concat " "
                        |> fun s -> s.Replace(" / ", "/", StringComparison.Ordinal).Replace(" , ", ", ", StringComparison.Ordinal)
                    Some(key, valueText, keyTokens, eqToken, valueTokens)
            else
                None
        | _ -> None

    /// <summary>Ports-block member line (ADR-0130): `input|output [stream|scalar] name: type`,
    /// `param name: type [= default] [bind column]`.</summary>
    let private tryParsePortLine (tokens: Token list) : (PortDeclRole * string option * string * string option * string option * string option) option =
        let sigTokens = significantTokens tokens

        let isDotted (segment: Token list) =
            not segment.IsEmpty
            && segment |> List.forall (fun t -> t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot)

        match sigTokens with
        | { Kind = TokenKind.Ident; Value = role } :: rest
            when String.Equals(role, "input", StringComparison.OrdinalIgnoreCase)
            || String.Equals(role, "output", StringComparison.OrdinalIgnoreCase)
            || String.Equals(role, "param", StringComparison.OrdinalIgnoreCase) ->
            let isParam = String.Equals(role, "param", StringComparison.OrdinalIgnoreCase)
            let roleEnum =
                if isParam then PortDeclRole.PortParam
                elif String.Equals(role, "output", StringComparison.OrdinalIgnoreCase) then PortDeclRole.PortOutput
                else PortDeclRole.PortInput

            let shape, afterShape =
                match rest with
                | { Kind = TokenKind.Ident; Value = s } :: tail
                    when String.Equals(s, "stream", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(s, "scalar", StringComparison.OrdinalIgnoreCase) -> Some(s.ToLowerInvariant()), tail
                | _ -> None, rest

            match afterShape with
            | { Kind = TokenKind.Ident; Value = portName } :: { Kind = TokenKind.Colon } :: valueRest
                when not (String.IsNullOrWhiteSpace portName) ->
                let valueRestAfterRows =
                    match valueRest with
                    | { Kind = TokenKind.Ident; Value = r } :: tail when String.Equals(r, "rows", StringComparison.OrdinalIgnoreCase) -> tail
                    | _ -> valueRest

                let valueTypeTokens = valueRestAfterRows |> List.takeWhile (fun t -> t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot)
                let trailing = valueRestAfterRows |> List.skip valueTypeTokens.Length

                if not (isDotted valueTypeTokens) then
                    None
                else
                    let valueType = valueTypeTokens |> List.map (fun t -> t.Value) |> String.concat ""
                    let mutable defaultValue = None
                    let mutable columnBinding = None
                    let mutable validTrailing = true

                    if isParam then
                        let readDottedFrom (rest: Token list) =
                            let columnTokens = rest |> List.takeWhile (fun t -> t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot)
                            let afterCol = rest |> List.skip columnTokens.Length
                            let column = columnTokens |> List.map (fun t -> t.Value) |> String.concat ""

                            if isDotted columnTokens && afterCol.IsEmpty then Some column else None

                        match trailing with
                        | [] -> ()
                        | { Kind = TokenKind.Eq } :: afterEq ->
                            match afterEq with
                            | { Kind = TokenKind.String; Value = s } :: bindRest ->
                                defaultValue <- Some s
                                match bindRest with
                                | [] -> ()
                                | { Kind = TokenKind.Ident; Value = b } :: columnRest
                                    when String.Equals(b, "bind", StringComparison.OrdinalIgnoreCase) ->
                                    match readDottedFrom columnRest with
                                    | Some col -> columnBinding <- Some col
                                    | None -> validTrailing <- false
                                | _ -> validTrailing <- false
                            | { Kind = TokenKind.Ident; Value = w } :: bindRest ->
                                defaultValue <- Some w
                                match bindRest with
                                | [] -> ()
                                | { Kind = TokenKind.Ident; Value = b } :: columnRest
                                    when String.Equals(b, "bind", StringComparison.OrdinalIgnoreCase) ->
                                    match readDottedFrom columnRest with
                                    | Some col -> columnBinding <- Some col
                                    | None -> validTrailing <- false
                                | _ -> validTrailing <- false
                            | _ -> validTrailing <- false
                        | { Kind = TokenKind.Ident; Value = b } :: columnRest
                            when String.Equals(b, "bind", StringComparison.OrdinalIgnoreCase) ->
                            match readDottedFrom columnRest with
                            | Some col -> columnBinding <- Some col
                            | None -> validTrailing <- false
                        | _ -> validTrailing <- false
                    elif not trailing.IsEmpty then
                        validTrailing <- false

                    if validTrailing then
                        Some(roleEnum, shape, portName, Some valueType, defaultValue, columnBinding)
                    else
                        None
            | _ -> None
        | _ -> None

    /// <summary>Parse a `from view|sql|xlsx …` body clause into <see cref="SourceOriginSyntax"/>.</summary>
    let private tryParseFromOrigin (afterFrom: Token list) : SourceOriginSyntax option =
        match afterFrom with
        | { Kind = TokenKind.Ident; Value = kind } :: rest
            when String.Equals(kind, "view", StringComparison.OrdinalIgnoreCase) ->
            match tryParseDottedName rest with
            | Some viewName -> Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.View, viewName, None))
            | None -> None
        | { Kind = TokenKind.Ident; Value = kind } :: { Kind = TokenKind.Ident; Value = mode } :: rest
            when String.Equals(kind, "sql", StringComparison.OrdinalIgnoreCase) ->
            if String.Equals(mode, "query", StringComparison.OrdinalIgnoreCase) then
                match rest with
                | [ { Kind = TokenKind.String; Value = body } ]
                | [ { Kind = TokenKind.Raw; Value = body } ] ->
                    Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.SqlQuery, body, None))
                | _ -> None
            elif String.Equals(mode, "file", StringComparison.OrdinalIgnoreCase) then
                match rest with
                | [ { Kind = TokenKind.String; Value = path } ] ->
                    Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.SqlFile, path, None))
                | _ -> None
            else
                None
        | { Kind = TokenKind.Ident; Value = kind } :: { Kind = TokenKind.Ident; Value = fileKw } :: { Kind = TokenKind.String; Value = path } :: rest
            when String.Equals(kind, "xlsx", StringComparison.OrdinalIgnoreCase)
            && String.Equals(fileKw, "file", StringComparison.OrdinalIgnoreCase) ->
            match rest with
            | [] -> Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.Xlsx, path, None))
            | [ { Kind = TokenKind.Ident; Value = sheetKw }; { Kind = TokenKind.String; Value = sheet } ]
                when String.Equals(sheetKw, "sheet", StringComparison.OrdinalIgnoreCase) ->
                Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.Xlsx, path, Some sheet))
            | _ -> None
        | _ -> None

    /// <summary>Parse a header `= Origin(…)` expression on a `source` block opener into <see cref="SourceOriginSyntax"/>.</summary>
    let private tryParseHeaderOriginExpr (tokensAfterEq: Token list) : SourceOriginSyntax option =
        // Strip trailing `{` or `{ ... }` if present on the header line
        let exprTokens =
            tokensAfterEq
            |> List.takeWhile (fun t -> t.Kind <> TokenKind.LBrace)

        match exprTokens with
        | { Kind = TokenKind.Ident; Value = ctor } :: { Kind = TokenKind.LParen } :: innerWithClose ->
            match List.rev innerWithClose with
            | { Kind = TokenKind.RParen } :: revArgs ->
                let argsTokens = List.rev revArgs
                if String.Equals(ctor, "view", StringComparison.OrdinalIgnoreCase) then
                    match tryParseDottedName argsTokens with
                    | Some viewName -> Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.View, viewName, None))
                    | None -> None
                elif String.Equals(ctor, "sqlquery", StringComparison.OrdinalIgnoreCase) then
                    match argsTokens with
                    | [ { Kind = TokenKind.String; Value = body } ]
                    | [ { Kind = TokenKind.Raw; Value = body } ] ->
                        Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.SqlQuery, body, None))
                    | _ -> None
                elif String.Equals(ctor, "sqlfile", StringComparison.OrdinalIgnoreCase) then
                    match argsTokens with
                    | [ { Kind = TokenKind.Ident; Value = path } ]
                    | [ { Kind = TokenKind.String; Value = path } ] ->
                        Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.SqlFile, path, None))
                    | _ -> None
                elif String.Equals(ctor, "xlsx", StringComparison.OrdinalIgnoreCase) then
                    match argsTokens with
                    | [ { Kind = TokenKind.Ident; Value = path } ]
                    | [ { Kind = TokenKind.String; Value = path } ] ->
                        Some(SourceOriginSyntax.Leaf(SourceLeafKindSyntax.Xlsx, path, None))
                    | _ -> None
                else
                    let compositeKind =
                        if String.Equals(ctor, "innerjoin", StringComparison.OrdinalIgnoreCase) then Some CompositeOriginKindSyntax.InnerJoin
                        elif String.Equals(ctor, "leftjoin", StringComparison.OrdinalIgnoreCase) then Some CompositeOriginKindSyntax.LeftJoin
                        elif String.Equals(ctor, "rightjoin", StringComparison.OrdinalIgnoreCase) then Some CompositeOriginKindSyntax.RightJoin
                        elif String.Equals(ctor, "fulljoin", StringComparison.OrdinalIgnoreCase) then Some CompositeOriginKindSyntax.FullJoin
                        elif String.Equals(ctor, "unionall", StringComparison.OrdinalIgnoreCase) then Some CompositeOriginKindSyntax.UnionAll
                        else None

                    match compositeKind, argsTokens with
                    | Some kind, [ { Kind = TokenKind.Ident; Value = left }; { Kind = TokenKind.Comma }; { Kind = TokenKind.Ident; Value = right } ] ->
                        Some(SourceOriginSyntax.Composite(kind, left, right))
                    | _ -> None
            | _ -> None
        | _ -> None

    /// <summary>Extract optional `: RowType` and `= Origin(…)` from a `source <id>` opener line.</summary>
    let private parseSourceHeaderMetadata (tokens: Token list) : string option * SourceOriginSyntax option * bool =
        let sigTokens =
            significantTokens tokens
            |> List.takeWhile (fun t -> t.Kind <> TokenKind.LBrace)

        match sigTokens with
        | { Kind = TokenKind.Ident; Value = kw } :: { Kind = TokenKind.Ident; Value = _sourceId } :: afterId
            when String.Equals(kw, "source", StringComparison.OrdinalIgnoreCase) ->
            let typeAnnotation, afterAnnotation, annotationOk =
                match afterId with
                | { Kind = TokenKind.Colon } :: { Kind = TokenKind.Ident; Value = rowType } :: rest ->
                    Some rowType, rest, true
                | { Kind = TokenKind.Colon } :: _ ->
                    None, [], false
                | _ ->
                    None, afterId, true

            if not annotationOk then
                None, None, false
            else
                match afterAnnotation with
                | [] -> typeAnnotation, None, true
                | { Kind = TokenKind.Eq } :: originTokens ->
                    match tryParseHeaderOriginExpr originTokens with
                    | Some origin -> typeAnnotation, Some origin, true
                    | None -> typeAnnotation, None, false
                | _ -> typeAnnotation, None, false
        | _ -> None, None, true

    /// <summary>Positional clause line (ADR-0130): dispatches `namespace`, `scope`, `use`,
    /// `from`, `default input|output`, and positional directives (`rows`, `catalog`, `entry`, `layout`)
    /// into structured <see cref="ClauseKind"/> variants.</summary>
    let private positionalDirectives =
        set [ "rows"; "catalog"; "entry"; "layout" ]

    let private tryParseClauseLine (tokens: Token list) : ClauseKind option =
        let sigTokens = significantTokens tokens

        if sigTokens |> List.exists (fun t -> t.Kind = TokenKind.Eq) then
            None
        else
            match sigTokens with
            | { Kind = TokenKind.Ident; Value = kw } :: rest when String.Equals(kw, "namespace", StringComparison.OrdinalIgnoreCase) ->
                match tryParseDottedName rest with
                | Some ns -> Some(ClauseKind.NamespaceClause ns)
                | None -> None
            | [ { Kind = TokenKind.Ident; Value = kw }; { Kind = TokenKind.Ident; Value = scopeKind } ]
                when String.Equals(kw, "scope", StringComparison.OrdinalIgnoreCase) ->
                Some(ClauseKind.ScopeClause(scopeKind.ToLowerInvariant()))
            | [ { Kind = TokenKind.Ident; Value = kw }; { Kind = TokenKind.Ident; Value = useKind }; { Kind = TokenKind.Ident; Value = target } ]
                when String.Equals(kw, "use", StringComparison.OrdinalIgnoreCase) ->
                Some(ClauseKind.UseClause(useKind.ToLowerInvariant(), target))
            | { Kind = TokenKind.Ident; Value = kw } :: rest
                when String.Equals(kw, "from", StringComparison.OrdinalIgnoreCase) ->
                tryParseFromOrigin rest |> Option.map ClauseKind.FromClause
            | [ { Kind = TokenKind.Ident; Value = kw }; { Kind = TokenKind.Ident; Value = dir }; { Kind = TokenKind.Ident; Value = portName } ]
                when String.Equals(kw, "default", StringComparison.OrdinalIgnoreCase)
                && (String.Equals(dir, "input", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(dir, "output", StringComparison.OrdinalIgnoreCase)) ->
                Some(ClauseKind.DefaultPortClause(dir.ToLowerInvariant(), portName))
            | { Kind = TokenKind.Ident; Value = kw } :: rest
                when positionalDirectives.Contains(kw.ToLowerInvariant()) && not rest.IsEmpty ->
                let args = rest |> List.map (fun t -> t.Value) |> Array.ofList
                Some(ClauseKind.DirectiveClause(kw.ToLowerInvariant(), args))
            | _ -> None

    /// <summary>Flow-link line (ADR-0130): `producer [outPort] -> [inPort] consumer` —
    /// the left side is endpoint-then-port, the right side is port-then-endpoint.</summary>
    let private tryParseFlowLinkLine (tokens: Token list) : (string * string option * string * string option) option =
        match tokens |> List.tryFindIndex (fun t -> t.Kind = TokenKind.FlowArrow) with
        | None -> None
        | Some arrowIdx ->
            let readDotted (segment: Token list) =
                let identTokens = segment |> List.takeWhile (fun t -> t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot)

                if identTokens.IsEmpty then
                    None
                elif (List.last identTokens).Kind = TokenKind.Dot then
                    None
                else
                    Some(identTokens |> List.map (fun t -> t.Value) |> String.concat "", segment |> List.skip identTokens.Length)

            let readBracketPort (segment: Token list) =
                match segment with
                | { Kind = TokenKind.LBracket } :: rest ->
                    match readDotted rest with
                    | Some(portName, { Kind = TokenKind.RBracket } :: afterBracket) ->
                        Some(portName, afterBracket)
                    | _ -> None
                | _ -> None

            let readLeft (segment: Token list) =
                match readDotted segment with
                | None -> None
                | Some(nodeName, []) -> Some(nodeName, None)
                | Some(nodeName, afterIdents) ->
                    match readBracketPort afterIdents with
                    | Some(port, []) -> Some(nodeName, Some port)
                    | _ -> None

            let readRight (segment: Token list) =
                match segment with
                | { Kind = TokenKind.LBracket } :: _ ->
                    match readBracketPort segment with
                    | Some(port, rest) ->
                        match readDotted rest with
                        | Some(nodeName, []) -> Some(nodeName, Some port)
                        | _ -> None
                    | None -> None
                | _ ->
                    match readDotted segment with
                    | Some(nodeName, []) -> Some(nodeName, None)
                    | _ -> None

            let sigTokens = significantTokens tokens
            match sigTokens |> List.tryFindIndex (fun t -> t.Kind = TokenKind.FlowArrow) with
            | None -> None
            | Some sigArrowIdx ->
                let leftSide, rightWithArrow = sigTokens |> List.splitAt sigArrowIdx
                let rightSide = rightWithArrow |> List.skip 1

                match readLeft leftSide, readRight rightSide with
                | Some(fromNode, fromPort), Some(toNode, toPort) -> Some(fromNode, fromPort, toNode, toPort)
                | _ -> None

    /// <summary>Grid drawing row (ADR-0111 §2 / ADR-0130): `[ ref:weight ] [ ]` —
    /// the empty cell is one whole `[]` token; the weight suffix rides verbatim.</summary>
    let private tryParseGridRowLine (tokens: Token list) : string[] option =
        let sigTokens = significantTokens tokens

        match sigTokens with
        | { Kind = TokenKind.EmptyCell } :: _ ->
            if sigTokens |> List.forall (fun t -> t.Kind = TokenKind.EmptyCell) then
                Some(sigTokens |> List.map (fun t -> t.Value) |> Array.ofList)
            else
                None
        | { Kind = TokenKind.LBracket } :: _ ->
            let cells = ResizeArray<string>()
            let mutable ok = true
            let mutable index = 0

            while ok && index < sigTokens.Length do
                match sigTokens.[index] with
                | { Kind = TokenKind.LBracket } ->
                    index <- index + 1

                    while ok && index < sigTokens.Length && sigTokens.[index].Kind <> TokenKind.RBracket do
                        match sigTokens.[index] with
                        | { Kind = TokenKind.EmptyCell; Value = empty } ->
                            cells.Add empty
                            index <- index + 1
                        | { Kind = TokenKind.Ident; Value = name } ->
                            let mutable cell = name
                            index <- index + 1

                            if index + 1 < sigTokens.Length
                               && sigTokens.[index].Kind = TokenKind.Colon
                               && sigTokens.[index + 1].Kind = TokenKind.Ident then
                                cell <- cell + ":" + sigTokens.[index + 1].Value
                                index <- index + 2

                            cells.Add cell
                        | _ -> ok <- false

                    if not ok || index >= sigTokens.Length || sigTokens.[index].Kind <> TokenKind.RBracket then
                        ok <- false
                    else
                        index <- index + 1
                | _ -> ok <- false

            if ok && cells.Count > 0 then Some(cells.ToArray()) else None
        | _ -> None

    /// <summary>Table row (ADR-0130): `| a | b |` — one TableRow token, cells split on `|`.</summary>
    let private tryParseTableRowLine (tokens: Token list) : string[] option =
        match tokens |> List.tryFind (fun t -> t.Kind = TokenKind.TableRow) with
        | None -> None
        | Some row ->
            let cells =
                row.Value.Split('|', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)

            if cells.Length = 0 then None else Some cells

    /// <summary>Module-header import line: `import &lt;kind&gt; from &lt;namespace&gt; [as &lt;alias&gt;]` (ADR-0098).</summary>
    let private tryParseImportLine (tokens: Token list) =
        let sigTokens = significantTokens tokens
        let keyword, rest =
            match sigTokens with
            | { Kind = TokenKind.Ident; Value = "import" } :: tail -> Some "import", tail
            | _ -> None, []

        match keyword with
        | None -> None
        | Some kw ->
            let kindOpt =
                match rest with
                | { Kind = TokenKind.Ident; Value = k } :: _
                    when not (String.Equals(k, "from", StringComparison.OrdinalIgnoreCase)) ->
                    Some k
                | _ -> None

            let afterFrom =
                match rest |> List.tryFindIndex (fun t -> t.Kind = TokenKind.Ident && String.Equals(t.Value, "from", StringComparison.OrdinalIgnoreCase)) with
                | Some i -> rest |> List.skip (i + 1)
                | None -> rest

            let targetTokens, aliasTokens =
                match afterFrom |> List.tryFindIndex (fun t -> t.Kind = TokenKind.Ident && String.Equals(t.Value, "as", StringComparison.OrdinalIgnoreCase)) with
                | Some i -> afterFrom |> List.splitAt i
                | None -> afterFrom, []

            let target =
                targetTokens
                |> List.filter (fun t -> t.Kind = TokenKind.String || t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot)
                |> List.map (fun t -> t.Value)
                |> String.concat ""

            let aliasName =
                aliasTokens
                |> List.tryPick (fun t ->
                    if t.Kind = TokenKind.Ident && not (String.Equals(t.Value, "as", StringComparison.OrdinalIgnoreCase)) then
                        Some t.Value
                    else
                        None)

            Some(kw, kindOpt, target, aliasName)

    let private appendModule (_parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (kind: string) (moduleId: string) =
        let directive =
            if String.Equals(kind, "section", StringComparison.OrdinalIgnoreCase) then
                DashSpecModuleDirective.Tab moduleId
            elif String.Equals(kind, "dashboard", StringComparison.OrdinalIgnoreCase) then
                DashSpecModuleDirective.Dashboard moduleId
            else
                DashSpecModuleDirective.Unit(kind, moduleId)

        mkNode (NodePayload.Module directive) (nextId id) line.Span (lineSyntaxTokens text allTokens line)

    let private appendBlock (_parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (kind: string) (blockId: string option) =
        let opener =
            match kind, blockId with
            | "grid", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid
            | "chrome", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Chrome
            | "click", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.OnClick
            | kind, id -> DashSpecBlockOpenerSyntax.ofLineKind(BlockSurfaceLineClassifier.BlockOpener(kind, id))

        let typeAnnotation, origin, headerValid =
            if String.Equals(kind, "source", StringComparison.OrdinalIgnoreCase) then
                parseSourceHeaderMetadata line.Tokens
            else
                None, None, true

        let payload =
            if headerValid then
                NodePayload.Block(opener, typeAnnotation, origin)
            else
                NodePayload.Line

        mkNode payload (nextId id) line.Span (lineSyntaxTokens text allTokens line)

    let private appendContentNode (container: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) =
        if isTypeParent container then
            match tryParseTypeFieldLine line.Tokens with
            | Some(optional, dashType, fieldName) ->
                container.Children.Add(
                    mkNode (NodePayload.TypeField(optional, dashType, fieldName)) (nextId id) line.Span (lineSyntaxTokens text allTokens line))
            | None ->
                appendLine container id text allTokens line
        elif isPortsParent container then
            let syntaxTokens = lineSyntaxTokens text allTokens line

            match tryParseClauseLine line.Tokens with
            | Some(ClauseKind.DefaultPortClause _ as kind) ->
                container.Children.Add(mkNode (NodePayload.Clause kind) (nextId id) line.Span syntaxTokens)
            | _ ->
                match tryParsePortLine line.Tokens with
                | Some(role, shape, portName, valueType, defaultValue, columnBinding) ->
                    container.Children.Add(
                        mkNode (NodePayload.Port(role, shape, portName, valueType, defaultValue, columnBinding)) (nextId id) line.Span syntaxTokens)
                | None ->
                    container.Children.Add(mkNode NodePayload.Line (nextId id) line.Span syntaxTokens)
        else
            let syntaxTokens = lineSyntaxTokens text allTokens line

            let gridRow =
                match line.Tokens |> significantTokens |> List.tryHead |> Option.map (fun t -> t.Kind) with
                | Some TokenKind.LBracket
                | Some TokenKind.EmptyCell -> tryParseGridRowLine line.Tokens
                | _ -> None

            let tableRow =
                if gridRow.IsNone
                   && line.Tokens |> List.exists (fun t -> t.Kind = TokenKind.TableRow) then
                    tryParseTableRowLine line.Tokens
                else
                    None

            let linkLine =
                if line.Tokens |> List.exists (fun t -> t.Kind = TokenKind.FlowArrow) then
                    tryParseFlowLinkLine line.Tokens
                else
                    None

            match gridRow with
            | Some cells ->
                container.Children.Add(mkNode (NodePayload.GridRow cells) (nextId id) line.Span syntaxTokens)
            | None ->
                match tableRow with
                | Some cells ->
                    container.Children.Add(mkNode (NodePayload.TableRow cells) (nextId id) line.Span syntaxTokens)
                | None ->
                    match linkLine with
                    | Some(fromNode, fromPort, toNode, toPort) ->
                        container.Children.Add(
                            mkNode (NodePayload.FlowLink(fromNode, fromPort, toNode, toPort)) (nextId id) line.Span syntaxTokens)
                    | None ->
                        match tryParseClauseLine line.Tokens with
                        | Some kind ->
                            container.Children.Add(mkNode (NodePayload.Clause kind) (nextId id) line.Span syntaxTokens)
                        | None ->
                            match tryParsePropertyLine syntaxTokens with
                            | Some(key, valueText, keyTokens, eqToken, valueTokens) ->
                                container.Children.Add(
                                    mkNode (NodePayload.Property(key, valueText, keyTokens, eqToken, valueTokens)) (nextId id) line.Span syntaxTokens)
                            | None ->
                                container.Children.Add(mkNode NodePayload.Line (nextId id) line.Span syntaxTokens)

    /// If a block opener line carries inline body tokens after `{` (e.g. `transform use to_zone { zone = UTC+3 }`
    /// or `transform use summarize { by = app_name`), extract that inline slice as a virtual PhysicalLine
    /// so its PropertyAssignment or ClauseDeclaration lands as the first child of the block.
    let private tryExtractInlineBodyAfterOpenBrace (line: TokenReaderPhysicalLines.PhysicalLine) : TokenReaderPhysicalLines.PhysicalLine option =
        let sigTokens = significantTokens line.Tokens
        match sigTokens |> List.tryFindIndex (fun t -> t.Kind = TokenKind.LBrace) with
        | None -> None
        | Some braceIdx ->
            let afterBrace =
                sigTokens
                |> List.skip (braceIdx + 1)
                |> List.filter (fun t -> t.Kind <> TokenKind.RBrace)

            if afterBrace.IsEmpty then
                None
            else
                let first = afterBrace.Head
                let last = List.last afterBrace
                let span = TextSpan.Create first.Start (max 1 (last.Start + max 1 last.Length - first.Start))
                Some { Span = span; Tokens = afterBrace }

    /// Does a `source` block opener expect a body on following lines?
    /// Composite origins without `{` on the header (`source all = UnionAll(a, b)`) are single-line declarations.
    let private blockOpenerHasBody (kind: string) (lineTokens: Token list) (braceOpens: int) (braceCloses: int) : bool * bool =
        if braceOpens > braceCloses then
            true, true // has body, closes on brace
        elif braceOpens > 0 && braceOpens = braceCloses then
            false, true // single-line `{ ... }` on the opener line itself
        elif String.Equals(kind, "source", StringComparison.OrdinalIgnoreCase) then
            let _, originOpt, _ = parseSourceHeaderMetadata lineTokens
            match originOpt with
            | Some(SourceOriginSyntax.Composite _) -> false, false // bodyless composite source
            | _ -> true, false
        elif String.Equals(kind, "transform", StringComparison.OrdinalIgnoreCase) then
            // `transform use <plugin>` without `{` on the opener line is a bodyless transform step
            false, false
        else
            true, false

    /// Parse block/module body until <c>reader</c> is exhausted, an <c>end</c> line, or
    /// a closing <c>}</c> — whichever kind of closer this container was opened with
    /// (ADR-0130: brace-opened blocks close on BraceClose, end-style on End lines).
    let rec private parseContainerBody
        (container: MutableNode)
        (id: int ref)
        (text: string)
        (allTokens: Token[])
        (reader: TokenReader)
        (closeContainer: bool)
        (closeOnBrace: bool)
        =
        let mutable continueParsing = true
        let mutable skipPendingUpdate = false
        let mutable pendingInnerBraces = 0

        while continueParsing && not reader.IsEofKeepingTrivia do
            match TokenReaderPhysicalLines.readNext reader with
            | None -> continueParsing <- false
            | Some line ->
                skipPendingUpdate <- false
                let lineKind = BlockSurfaceLineClassifier.classifyLineTokens line.Tokens

                let braceOpens = line.Tokens |> List.filter (fun t -> t.Kind = TokenKind.LBrace) |> List.length
                let braceCloses = line.Tokens |> List.filter (fun t -> t.Kind = TokenKind.RBrace) |> List.length

                let closesThisContainer =
                    closeOnBrace
                    && pendingInnerBraces = 0
                    && braceCloses > braceOpens

                match lineKind with
                | BlockSurfaceLineClassifier.Blank -> appendTriviaOrBlank container id text allTokens line
                | _ ->
                    let cardRef =
                        if isCardsParent container then
                            match line.Tokens with
                            | [ { Kind = TokenKind.Ident; Value = cardId } ] -> Some cardId
                            | _ -> None
                        else
                            None

                    match cardRef with
                    | Some cardId -> appendCardRef container id text allTokens line cardId
                    | None ->
                        match lineKind with
                        | BlockSurfaceLineClassifier.ModuleHeader(kind, moduleId) ->
                            let moduleNode = appendModule container id text allTokens line kind moduleId
                            container.Children.Add moduleNode
                            parseContainerBody moduleNode id text allTokens reader true false
                        | BlockSurfaceLineClassifier.BlockOpener(kind, blockId) ->
                            let blockNode = appendBlock container id text allTokens line kind blockId
                            container.Children.Add blockNode

                            match blockNode.Payload with
                            | NodePayload.Line ->
                                // Malformed block header stays a Line node
                                ()
                            | _ ->
                                match tryExtractInlineBodyAfterOpenBrace line with
                                | Some inlineLine ->
                                    appendContentNode blockNode id text allTokens inlineLine
                                | None -> ()

                                let hasBody, braceOpened = blockOpenerHasBody kind line.Tokens braceOpens braceCloses

                                if hasBody then
                                    parseContainerBody blockNode id text allTokens reader true braceOpened

                                skipPendingUpdate <- true
                        | BlockSurfaceLineClassifier.End(kind, endId) ->
                            appendEndBlock container id text allTokens line kind endId

                            if closeContainer && not closeOnBrace then
                                continueParsing <- false
                        | BlockSurfaceLineClassifier.Blank -> ()
                        | BlockSurfaceLineClassifier.BraceOpen
                        | BlockSurfaceLineClassifier.BraceClose -> ()
                        | BlockSurfaceLineClassifier.Content ->
                            match tryParseImportLine line.Tokens with
                            | Some(keyword, kindOpt, target, aliasName) ->
                                appendImport container id text allTokens line keyword kindOpt target aliasName
                            | None ->
                                appendContentNode container id text allTokens line

                if closeOnBrace && not skipPendingUpdate then
                    pendingInnerBraces <- max 0 (pendingInnerBraces + braceOpens - braceCloses)

                if closesThisContainer then
                    continueParsing <- false

    let parse (text: string) : ParseTree =
        let allTokens = DashSpecLexer.tokenize text |> Array.ofSeq
        let reader = TokenReader(allTokens, text)
        let id = ref 1

        let root =
            mkNode NodePayload.CompilationUnit (nextId id) (TextSpan.Create 0 (max 0 text.Length)) Array.empty

        parseContainerBody root id text allTokens reader false false

        let syntaxTokens =
            allTokens
            |> Array.choose (fun token ->
                match token.Kind with
                | TokenKind.Newline | TokenKind.Eof -> None
                | _ -> Some token)
            |> Array.mapi (fun _index token ->
                SyntaxTokenClassifier.toSyntaxToken text allTokens (tokenIndex allTokens token) token)

        { Text = text
          Root = toImmutable root
          Tokens = syntaxTokens }
