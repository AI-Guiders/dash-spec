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
        | Port of role: PortDeclRole * shape: PortShapeSyntax option * portName: string * valueType: string option * defaultValue: string option * columnBinding: string option
        | FlowLink of fromNode: string * fromPort: string option * toNode: string * toPort: string option
        | Clause of kind: ClauseKind
        | GridRow of kind: GridRowKind * typedCells: GridCellSyntax[] * cells: string[]
        | TableRow of kind: TableRowKind * cells: string[]
        | CustomNode of node: DashSpecAstNode
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
            let resolvedImportKind = kind |> Option.bind ImportKindRegistry.tryFindByKeyword
            DashSpecAstNode.ImportDeclaration
                { Id = node.Id
                  Span = span
                  Keyword = keyword
                  Kind = kind
                  ImportKind = resolvedImportKind
                  TargetPath = DotSyntax.parse target
                  Target = target
                  Alias = aliasName
                  Tokens = node.Tokens }
        | NodePayload.TypeField(optional, fieldType, fieldName) ->
            DashSpecAstNode.TypeFieldDeclaration
                { Id = node.Id
                  Span = span
                  Optional = optional
                  FieldType = fieldType
                  FieldPath = DotSyntax.parse fieldName
                  FieldName = fieldName
                  Tokens = node.Tokens }
        | NodePayload.Property(key, valueText, keyTokens, eqToken, valueTokens) ->
            let rec parseAtom (toks: SyntaxToken[]) (fallbackText: string) : PropertyValueSyntax =
                if toks.Length = 0 then
                    PropertyValueSyntax.Raw fallbackText
                elif toks.Length >= 3
                     && toks.[toks.Length - 1].LexKind = TokenKind.String
                     && toks.[toks.Length - 2].LexKind = TokenKind.Ident
                     && String.Equals(toks.[toks.Length - 2].Text, "as", StringComparison.OrdinalIgnoreCase) then
                    let innerToks = toks.[0 .. toks.Length - 3]
                    let innerText = innerToks |> Array.map (fun t -> t.Text) |> String.concat " "
                    let rawTitle = toks.[toks.Length - 1].Text
                    let title =
                        if rawTitle.Length >= 2 && rawTitle.[0] = '"' && rawTitle.[rawTitle.Length - 1] = '"' then
                            rawTitle.Substring(1, rawTitle.Length - 2)
                        else
                            rawTitle
                    PropertyValueSyntax.Aliased(parseAtom innerToks innerText, title)
                elif toks.Length >= 2
                     && toks.[0].LexKind = TokenKind.LBracket
                     && toks.[toks.Length - 1].LexKind = TokenKind.RBracket then
                    let inner = toks.[1 .. toks.Length - 2]
                    let items = ResizeArray<PropertyValueSyntax>()
                    let cur = ResizeArray<SyntaxToken>()
                    let mutable depth = 0
                    for t in inner do
                        if t.LexKind = TokenKind.LParen || t.LexKind = TokenKind.LBracket then
                            depth <- depth + 1
                            cur.Add t
                        elif t.LexKind = TokenKind.RParen || t.LexKind = TokenKind.RBracket then
                            depth <- depth - 1
                            cur.Add t
                        elif t.LexKind = TokenKind.Comma && depth = 0 then
                            if cur.Count > 0 then
                                let arr = cur.ToArray()
                                items.Add(parseAtom arr (arr |> Array.map (fun x -> x.Text) |> String.concat ""))
                                cur.Clear()
                        else
                            cur.Add t
                    if cur.Count > 0 then
                        let arr = cur.ToArray()
                        items.Add(parseAtom arr (arr |> Array.map (fun x -> x.Text) |> String.concat ""))
                    PropertyValueSyntax.BracketList(items.ToArray())
                elif toks |> Array.exists (fun t -> t.LexKind = TokenKind.DotDot) then
                    let idx = toks |> Array.findIndex (fun t -> t.LexKind = TokenKind.DotDot)
                    let left = toks.[0 .. idx - 1]
                    let right = toks.[idx + 1 ..]
                    let leftText = left |> Array.map (fun x -> x.Text) |> String.concat ""
                    let rightText = right |> Array.map (fun x -> x.Text) |> String.concat ""
                    match DateRangeValue.tryParseBounds leftText rightText with
                    | Result.Ok dr -> PropertyValueSyntax.DateRange dr
                    | Result.Error _ ->
                        PropertyValueSyntax.Range(
                            parseAtom left leftText,
                            parseAtom right rightText)
                elif toks |> Array.exists (fun t -> t.LexKind = TokenKind.Comma)
                     && not (toks |> Array.exists (fun t -> t.LexKind = TokenKind.LParen)) then
                    let items = ResizeArray<PropertyValueSyntax>()
                    let cur = ResizeArray<SyntaxToken>()
                    for t in toks do
                        if t.LexKind = TokenKind.Comma then
                            if cur.Count > 0 then
                                let arr = cur.ToArray()
                                items.Add(parseAtom arr (arr |> Array.map (fun x -> x.Text) |> String.concat " "))
                                cur.Clear()
                        else
                            cur.Add t
                    if cur.Count > 0 then
                        let arr = cur.ToArray()
                        items.Add(parseAtom arr (arr |> Array.map (fun x -> x.Text) |> String.concat " "))
                    PropertyValueSyntax.CommaList(items.ToArray())
                elif toks.Length >= 3
                     && toks.[toks.Length - 1].LexKind = TokenKind.RParen
                     && toks |> Array.exists (fun t -> t.LexKind = TokenKind.LParen) then
                    let lpIdx = toks |> Array.findIndex (fun t -> t.LexKind = TokenKind.LParen)
                    let fnToks = toks.[0 .. lpIdx - 1]
                    let argToks = toks.[lpIdx + 1 .. toks.Length - 2]
                    if fnToks.Length > 0 && fnToks |> Array.forall (fun t -> t.LexKind = TokenKind.Ident || t.LexKind = TokenKind.Dot) then
                        let fnPath = DotSyntax.parse (fnToks |> Array.map (fun t -> t.Text) |> String.concat "")
                        let args =
                            if argToks.Length = 0 then [||]
                            else [| parseAtom argToks (argToks |> Array.map (fun t -> t.Text) |> String.concat "") |]
                        PropertyValueSyntax.Call(fnPath, args)
                    else
                        PropertyValueSyntax.Raw fallbackText
                elif toks.Length = 1 then
                    let t = toks.[0]
                    match t.LexKind with
                    | TokenKind.String ->
                        let raw = t.Text
                        let s =
                            if raw.Length >= 2 && raw.[0] = '"' && raw.[raw.Length - 1] = '"' then
                                raw.Substring(1, raw.Length - 2)
                            else
                                raw
                        PropertyValueSyntax.StringLiteral s
                    | TokenKind.HexColor ->
                        match HexColorValue.tryParse t.Text with
                        | Result.Ok c -> PropertyValueSyntax.HexColor c
                        | Result.Error _ -> PropertyValueSyntax.Raw fallbackText
                    | TokenKind.TimeShift ->
                        match TimeShiftValue.tryParse t.Text with
                        | Result.Ok s -> PropertyValueSyntax.TimeShift s
                        | Result.Error _ -> PropertyValueSyntax.Raw fallbackText
                    | TokenKind.IanaZone ->
                        match IanaZoneValue.tryParse t.Text with
                        | Result.Ok z -> PropertyValueSyntax.IanaZone z
                        | Result.Error _ -> PropertyValueSyntax.Raw fallbackText
                    | TokenKind.RelativeDay ->
                        match RelativeDayValue.tryParse t.Text with
                        | Result.Ok r -> PropertyValueSyntax.RelativeDay r
                        | Result.Error _ -> PropertyValueSyntax.Raw fallbackText
                    | TokenKind.Ident ->
                        let txt = t.Text
                        if String.Equals(txt, "true", StringComparison.OrdinalIgnoreCase) then
                            PropertyValueSyntax.Boolean true
                        elif String.Equals(txt, "false", StringComparison.OrdinalIgnoreCase) then
                            PropertyValueSyntax.Boolean false
                        elif String.Equals(txt, "today", StringComparison.OrdinalIgnoreCase) then
                            PropertyValueSyntax.DateBound DateBoundValue.Today
                        else
                            match Int64.TryParse(txt, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture) with
                            | true, i -> PropertyValueSyntax.Integer i
                            | false, _ ->
                                match Decimal.TryParse(txt, Globalization.NumberStyles.Number, Globalization.CultureInfo.InvariantCulture) with
                                | true, d -> PropertyValueSyntax.Decimal d
                                | false, _ ->
                                    match DateBoundValue.tryParse txt with
                                    | Result.Ok b -> PropertyValueSyntax.DateBound b
                                    | Result.Error _ -> PropertyValueSyntax.Member(DotSyntax.Ident txt)
                    | _ -> PropertyValueSyntax.Raw fallbackText
                elif toks |> Array.forall (fun t -> t.LexKind = TokenKind.Ident || t.LexKind = TokenKind.Dot) then
                    PropertyValueSyntax.Member(DotSyntax.parse (toks |> Array.map (fun t -> t.Text) |> String.concat ""))
                else
                    PropertyValueSyntax.Raw fallbackText

            DashSpecAstNode.PropertyAssignment
                { Id = node.Id
                  Span = span
                  KeyPath = DotSyntax.parse key
                  Key = key
                  Value = parseAtom valueTokens valueText
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
            let sourceEndpoint = FlowEndpointSyntax.create fromNode fromPort
            let targetEndpoint = FlowEndpointSyntax.create toNode toPort
            DashSpecAstNode.FlowLinkDeclaration
                { Id = node.Id
                  Span = span
                  Source = sourceEndpoint
                  Target = targetEndpoint
                  FromPath = sourceEndpoint.Path
                  FromNode = fromNode
                  FromPort = fromPort
                  ToPath = targetEndpoint.Path
                  ToNode = toNode
                  ToPort = toPort
                  Tokens = node.Tokens }
        | NodePayload.Clause kind ->
            DashSpecAstNode.ClauseDeclaration
                { Id = node.Id
                  Span = span
                  Kind = kind
                  Tokens = node.Tokens }
        | NodePayload.GridRow(kind, typedCells, cells) ->
            DashSpecAstNode.GridRowDeclaration
                { Id = node.Id
                  Span = span
                  Kind = kind
                  TypedCells = typedCells
                  Cells = cells
                  Tokens = node.Tokens }
        | NodePayload.TableRow(kind, cells) ->
            DashSpecAstNode.TableRowDeclaration
                { Id = node.Id
                  Span = span
                  Kind = kind
                  Cells = cells
                  Tokens = node.Tokens }
        | NodePayload.CustomNode custom -> custom
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

    let private tryParseDotSyntax (tokens: Token list) : DotSyntax option =
        let filtered = significantTokens tokens

        if filtered.IsEmpty then
            None
        elif filtered |> List.forall (fun t -> t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot) then
            let joined = filtered |> List.map (fun t -> t.Value) |> String.concat ""
            if String.IsNullOrWhiteSpace joined || joined.StartsWith('.') || joined.EndsWith('.') then
                None
            else
                Some(DotSyntax.parse joined)
        else
            None

    let private tryParseDottedName (tokens: Token list) =
        tryParseDotSyntax tokens |> Option.map (fun d -> d.Text)

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

            let validKey =
                (keyTokens |> Array.forall (fun t -> t.LexKind = TokenKind.Ident || t.LexKind = TokenKind.Dot))
                || (keyTokens.Length = 1 && keyTokens.[0].LexKind = TokenKind.String)

            if validKey then
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
    let private tryParsePortLine (tokens: Token list) : (PortDeclRole * PortShapeSyntax option * string * string option * string option * string option) option =
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
                    when String.Equals(s, "stream", StringComparison.OrdinalIgnoreCase) ->
                    Some PortShapeSyntax.Stream, tail
                | { Kind = TokenKind.Ident; Value = s } :: tail
                    when String.Equals(s, "scalar", StringComparison.OrdinalIgnoreCase) ->
                    Some PortShapeSyntax.Scalar, tail
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

    let private tryParseScopeSyntax (raw: string) : LayoutScopeSyntax option =
        match raw.Trim().ToLowerInvariant() with
        | "toolbar" -> Some LayoutScopeSyntax.Toolbar
        | "section" -> Some LayoutScopeSyntax.Section
        | "page" -> Some LayoutScopeSyntax.Page
        | "card" -> Some LayoutScopeSyntax.Card
        | "host" -> Some LayoutScopeSyntax.Host
        | _ -> None

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
                tryParseScopeSyntax scopeKind |> Option.map ClauseKind.ScopeClause
            | [ { Kind = TokenKind.Ident; Value = kw }; { Kind = TokenKind.Ident; Value = useKind }; { Kind = TokenKind.Ident; Value = target } ]
                when String.Equals(kw, "use", StringComparison.OrdinalIgnoreCase) ->
                if String.Equals(useKind, "provider", StringComparison.OrdinalIgnoreCase) then
                    let binding =
                        if String.Equals(target, "infer", StringComparison.OrdinalIgnoreCase) then
                            ProviderBindingSyntax.Infer
                        else
                            ProviderBindingSyntax.Named target
                    Some(ClauseKind.UseProviderClause binding)
                else
                    Some(ClauseKind.UseClause(useKind.ToLowerInvariant(), target))
            | { Kind = TokenKind.Ident; Value = kw } :: rest
                when String.Equals(kw, "from", StringComparison.OrdinalIgnoreCase) ->
                tryParseFromOrigin rest |> Option.map ClauseKind.FromClause
            | [ { Kind = TokenKind.Ident; Value = kw }; { Kind = TokenKind.Ident; Value = dir }; { Kind = TokenKind.Ident; Value = portName } ]
                when String.Equals(kw, "default", StringComparison.OrdinalIgnoreCase)
                && (String.Equals(dir, "input", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(dir, "output", StringComparison.OrdinalIgnoreCase)) ->
                let portDir =
                    if String.Equals(dir, "input", StringComparison.OrdinalIgnoreCase) then PortDirection.Input
                    else PortDirection.Output
                Some(ClauseKind.DefaultPortClause(portDir, portName))
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

    /// <summary>Grid drawing row (ADR-0111 §2 / ADR-0130 / ADR-0131): `[ ref:weight ] [ ]` —
    /// first-class `GridRowKind` (`RulerRow` vs `BoardRow`) and `GridCellTarget` (`EmptySlot` vs `Slot`).</summary>
    let private tryParseGridRowLine (tokens: Token list) : (GridRowKind * GridCellSyntax[] * string[]) option =
        let sigTokens = significantTokens tokens

        match sigTokens with
        | { Kind = TokenKind.EmptyCell } :: _ ->
            if sigTokens |> List.forall (fun t -> t.Kind = TokenKind.EmptyCell) then
                let typed =
                    sigTokens
                    |> List.map (fun _ -> { Target = GridCellTarget.EmptySlot; Weight = None })
                    |> Array.ofList
                let raw = sigTokens |> List.map (fun t -> t.Value) |> Array.ofList
                Some(GridRowKind.RulerRow typed.Length, typed, raw)
            else
                None
        | { Kind = TokenKind.LBracket } :: _ ->
            let typedCells = ResizeArray<GridCellSyntax>()
            let rawCells = ResizeArray<string>()
            let mutable ok = true
            let mutable index = 0

            while ok && index < sigTokens.Length do
                match sigTokens.[index] with
                | { Kind = TokenKind.LBracket } ->
                    index <- index + 1

                    while ok && index < sigTokens.Length && sigTokens.[index].Kind <> TokenKind.RBracket do
                        let readWeightAndAppend (cellTarget: GridCellTarget) (targetText: string) =
                            let mutable rawCell = targetText
                            let mutable weightOpt: int option = None

                            if index + 1 < sigTokens.Length
                               && sigTokens.[index].Kind = TokenKind.Colon
                               && sigTokens.[index + 1].Kind = TokenKind.Ident then
                                let weightText = sigTokens.[index + 1].Value
                                rawCell <- targetText + ":" + weightText
                                match Int32.TryParse weightText with
                                | true, w -> weightOpt <- Some w
                                | _ -> ()
                                index <- index + 2

                            typedCells.Add { Target = cellTarget; Weight = weightOpt }
                            rawCells.Add rawCell

                        match sigTokens.[index] with
                        | { Kind = TokenKind.EmptyCell; Value = empty } ->
                            index <- index + 1
                            readWeightAndAppend GridCellTarget.EmptySlot empty
                        | { Kind = TokenKind.Ident; Value = name } ->
                            index <- index + 1
                            readWeightAndAppend (GridCellTarget.Slot(DotSyntax.parse name)) name
                        | _ -> ok <- false

                    if not ok || index >= sigTokens.Length || sigTokens.[index].Kind <> TokenKind.RBracket then
                        ok <- false
                    else
                        index <- index + 1
                | _ -> ok <- false

            if ok && typedCells.Count > 0 then
                let cellsArr = typedCells.ToArray()
                Some(GridRowKind.BoardRow cellsArr, cellsArr, rawCells.ToArray())
            else
                None
        | _ -> None

    /// <summary>Table row (ADR-0130): `| a | b |` — one TableRow token, cells split on `|`.</summary>
    let private tryParseTableRowLine (tokens: Token list) : string[] option =
        match tokens |> List.tryFind (fun t -> t.Kind = TokenKind.TableRow) with
        | None -> None
        | Some row ->
            let cells =
                row.Value.Split('|', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)

            if cells.Length = 0 then None else Some cells

    let private isTableSeparatorRow (cells: string[]) =
        cells.Length > 0
        && cells |> Array.forall (fun c -> c.Length > 0 && c |> Seq.forall (fun ch -> ch = '-' || ch = ':'))

    let private classifyTableRow (containerOpener: DashSpecBlockOpener option) (existingHeader: string[] option) (cells: string[]) : TableRowKind =
        match existingHeader with
        | None ->
            TableRowKind.Header(cells |> Array.map (fun c -> c.Trim()))
        | Some _ when isTableSeparatorRow cells ->
            TableRowKind.Separator
        | Some headerCols ->
            let attrs =
                [ for i in 0 .. (min (cells.Length - 1) (headerCols.Length - 1)) do
                    let key = headerCols.[i].Trim().ToLowerInvariant()
                    let v = cells.[i].Trim()
                    if key.Length > 0 && v.Length > 0 && v <> "-" then
                        yield key, v ]
                |> Map.ofList

            let tryAttr name = Map.tryFind name attrs

            let blockKeyword =
                match containerOpener with
                | Some(DashSpecBlockOpener.Anonymous kw)
                | Some(DashSpecBlockOpener.Named(kw, _)) -> Some kw
                | None -> None

            match blockKeyword with
            | Some DashSpecBlockKeyword.Filters ->
                TableRowKind.Filter
                    { Id = tryAttr "id" |> Option.defaultValue ""
                      Bind = tryAttr "bind" |> Option.defaultValue ""
                      Column = tryAttr "column"
                      Label = tryAttr "label"
                      Widget = tryAttr "widget"
                      Attributes = attrs }
            | Some DashSpecBlockKeyword.Links ->
                let id = tryAttr "id" |> Option.defaultValue ""
                TableRowKind.Link
                    { Id = id
                      Label = tryAttr "label" |> Option.defaultValue id
                      Url = tryAttr "url" |> Option.defaultValue ""
                      Target = tryAttr "target"
                      Topbar = tryAttr "topbar"
                      Settings = tryAttr "settings" }
            | Some DashSpecBlockKeyword.OnClick
            | Some DashSpecBlockKeyword.Click ->
                TableRowKind.Click
                    { Source = tryAttr "source" |> Option.defaultValue ""
                      Drill = tryAttr "drill"
                      Set = tryAttr "set" }
            | Some(DashSpecBlockKeyword.Other kw) when String.Equals(kw, "when", StringComparison.OrdinalIgnoreCase) ->
                TableRowKind.When
                    { State = tryAttr "state" |> Option.defaultValue ""
                      Message = tryAttr "message" |> Option.defaultValue "" }
            | Some(DashSpecBlockKeyword.Other kw)
                when String.Equals(kw, "table", StringComparison.OrdinalIgnoreCase)
                     || String.Equals(kw, "columns", StringComparison.OrdinalIgnoreCase) ->
                TableRowKind.Column
                    { Id = tryAttr "id" |> Option.defaultValue ""
                      Format = tryAttr "format"
                      Label = tryAttr "label"
                      Sorting = tryAttr "sorting"
                      Alignment = tryAttr "alignment"
                      HeaderAlignment = tryAttr "header_alignment" }
            | _ when Map.containsKey "dashboard" attrs || Map.containsKey "dashspec" attrs ->
                let id = tryAttr "id" |> Option.defaultValue ""
                TableRowKind.CatalogEntry
                    { Id = id
                      Title = tryAttr "title" |> Option.orElse (tryAttr "label") |> Option.defaultValue id
                      DashspecPath = tryAttr "dashboard" |> Option.orElse (tryAttr "dashspec") |> Option.defaultValue ""
                      Section = tryAttr "section"
                      Page = tryAttr "page" }
            | _ ->
                TableRowKind.Generic attrs

    /// <summary>Module-header import line: `import &lt;kind&gt; from &lt;namespace&gt; [= &lt;alias&gt;]` or `from &lt;namespace&gt; import &lt;kind&gt; [= &lt;alias&gt;]`.</summary>
    let private tryParseImportLine (tokens: Token list) =
        let sigTokens = significantTokens tokens
        match sigTokens with
        | { Kind = TokenKind.Ident; Value = "from" } :: rest when
            rest |> List.exists (fun t -> t.Kind = TokenKind.Ident && String.Equals(t.Value, "import", StringComparison.OrdinalIgnoreCase)) ->
            let importIdx =
                rest
                |> List.findIndex (fun t -> t.Kind = TokenKind.Ident && String.Equals(t.Value, "import", StringComparison.OrdinalIgnoreCase))
            let nsTokens = rest |> List.take importIdx
            let afterImport =
                rest
                |> List.skip (importIdx + 1)
                |> List.filter (fun t -> t.Kind <> TokenKind.LBrace && t.Kind <> TokenKind.RBrace)
            let target =
                nsTokens
                |> List.filter (fun t -> t.Kind = TokenKind.String || t.Kind = TokenKind.Ident || t.Kind = TokenKind.Dot)
                |> List.map (fun t -> t.Value)
                |> String.concat ""
            match afterImport with
            | { Kind = TokenKind.Ident; Value = k } :: tail ->
                let aliasName =
                    match tail with
                    | { Kind = TokenKind.Eq } :: { Kind = TokenKind.Ident; Value = a } :: _ -> Some a
                    | { Kind = TokenKind.Ident; Value = asKw } :: { Kind = TokenKind.Ident; Value = a } :: _
                        when String.Equals(asKw, "as", StringComparison.OrdinalIgnoreCase) -> Some a
                    | _ -> None
                Some("import", Some k, target, aliasName)
            | _ -> None
        | { Kind = TokenKind.Ident; Value = "import" } :: rest ->
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

            let isAliasSplitToken (t: Token) =
                t.Kind = TokenKind.Eq
                || (t.Kind = TokenKind.Ident && String.Equals(t.Value, "as", StringComparison.OrdinalIgnoreCase))

            let targetTokens, aliasTokens =
                match afterFrom |> List.tryFindIndex isAliasSplitToken with
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

            Some("import", kindOpt, target, aliasName)
        | _ -> None

    let private appendModule (_parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (kind: string) (moduleId: string) =
        let emitViews =
            significantTokens line.Tokens
            |> List.skip 3
            |> List.exists (fun t -> t.Kind = TokenKind.Ident && String.Equals(t.Value, "view", StringComparison.OrdinalIgnoreCase))

        let directive =
            if String.Equals(kind, "section", StringComparison.OrdinalIgnoreCase) then
                DashSpecModuleDirective.Tab moduleId
            elif String.Equals(kind, "dashboard", StringComparison.OrdinalIgnoreCase) then
                DashSpecModuleDirective.Dashboard moduleId
            else
                DashSpecModuleDirective.Module(DashSpecModuleKind.ofKeyword kind emitViews, moduleId)

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

    /// Context passed to a registered surface line-grammar plugin.
    type LineGrammarContext =
        { ContainerOpener: DashSpecBlockOpener option
          ExistingTableHeader: string[] option
          NodeId: NodeId
          Span: TextSpan
          RawTokens: Token list
          SyntaxTokens: SyntaxToken[] }

    type LineGrammarDescriptor =
        { Id: string
          TryParse: LineGrammarContext -> DashSpecAstNode option }

    let private lineGrammarOrder = ResizeArray<string>()
    let private customLineGrammars = Dictionary<string, LineGrammarDescriptor>(StringComparer.OrdinalIgnoreCase)

    /// Register or replace a pluggable line-grammar contributor in the surface syntax microkernel.
    let registerLineGrammar (descriptor: LineGrammarDescriptor) =
        if not (String.IsNullOrWhiteSpace descriptor.Id) then
            let key = descriptor.Id.Trim()
            if not (customLineGrammars.ContainsKey key) then
                lineGrammarOrder.Add key
            customLineGrammars.[key] <- descriptor

    /// Pure surface-syntax kernel dispatcher: evaluates registered line-grammar plugins,
    /// falling back to `key = value` (`PropertyAssignment`) or raw `Line`.
    let private appendContentNode (container: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) =
        let syntaxTokens = lineSyntaxTokens text allTokens line
        let containerOpener =
            match container.Payload with
            | NodePayload.Block(opener, _, _) -> Some opener
            | _ -> None

        let existingTableHeader =
            container.Children
            |> Seq.tryPick (fun child ->
                match child.Payload with
                | NodePayload.TableRow(TableRowKind.Header cols, _)
                | NodePayload.CustomNode(DashSpecAstNode.TableRowDeclaration { Kind = TableRowKind.Header cols }) -> Some cols
                | _ -> None)

        let candidateId = nextId id
        let ctx =
            { ContainerOpener = containerOpener
              ExistingTableHeader = existingTableHeader
              NodeId = candidateId
              Span = line.Span
              RawTokens = line.Tokens
              SyntaxTokens = syntaxTokens }

        let grammarHit =
            lineGrammarOrder
            |> Seq.tryPick (fun key -> customLineGrammars.[key].TryParse ctx)

        match grammarHit with
        | Some customNode ->
            container.Children.Add(mkNode (NodePayload.CustomNode customNode) (DashSpecAst.id customNode) line.Span syntaxTokens)
        | None ->
            match containerOpener with
            | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Type)
            | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, _))
            | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports)
            | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _)) ->
                container.Children.Add(mkNode NodePayload.Line candidateId line.Span syntaxTokens)
            | _ ->
                match tryParsePropertyLine syntaxTokens with
                | Some(key, valueText, keyTokens, eqToken, valueTokens) ->
                    container.Children.Add(
                        mkNode (NodePayload.Property(key, valueText, keyTokens, eqToken, valueTokens)) candidateId line.Span syntaxTokens)
                | None ->
                    container.Children.Add(mkNode NodePayload.Line candidateId line.Span syntaxTokens)

    do
        // 1. Type field line grammar (`?type name` inside `type` blocks).
        registerLineGrammar
            { Id = "types.field"
              TryParse = fun ctx ->
                  match ctx.ContainerOpener with
                  | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Type)
                  | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, _)) ->
                      match tryParseTypeFieldLine ctx.RawTokens with
                      | Some(optional, dashType, fieldName) ->
                          Some(
                              DashSpecAstNode.TypeFieldDeclaration
                                  { Id = ctx.NodeId
                                    Span = ctx.Span
                                    Optional = optional
                                    FieldType = dashType
                                    FieldPath = DotSyntax.parse fieldName
                                    FieldName = fieldName
                                    Tokens = ctx.SyntaxTokens })
                      | None -> None
                  | _ -> None }

        // 2. Port declaration line grammar (`input/output/param` and `default input|output` inside `ports` blocks).
        registerLineGrammar
            { Id = "ports.declaration"
              TryParse = fun ctx ->
                  match ctx.ContainerOpener with
                  | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports)
                  | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _)) ->
                      match tryParseClauseLine ctx.RawTokens with
                      | Some(ClauseKind.DefaultPortClause _ as kind) ->
                          Some(
                              DashSpecAstNode.ClauseDeclaration
                                  { Id = ctx.NodeId
                                    Span = ctx.Span
                                    Kind = kind
                                    Tokens = ctx.SyntaxTokens })
                      | _ ->
                          match tryParsePortLine ctx.RawTokens with
                          | Some(role, shape, portName, valueType, defaultValue, columnBinding) ->
                              Some(
                                  DashSpecAstNode.PortDeclaration
                                      { Id = ctx.NodeId
                                        Span = ctx.Span
                                        Role = role
                                        PortShape = shape
                                        PortName = portName
                                        ValueType = valueType
                                        DefaultValue = defaultValue
                                        ColumnBinding = columnBinding
                                        Tokens = ctx.SyntaxTokens })
                          | None -> None
                  | _ -> None }

        // 3. Bracket/grid layout line grammar (`[] [] …` and `[ ref:weight [] … ]`).
        registerLineGrammar
            { Id = "layout.grid_row"
              TryParse = fun ctx ->
                  match ctx.RawTokens |> significantTokens |> List.tryHead |> Option.map (fun t -> t.Kind) with
                  | Some TokenKind.LBracket
                  | Some TokenKind.EmptyCell ->
                      match tryParseGridRowLine ctx.RawTokens with
                      | Some(kind, typedCells, cells) ->
                          Some(
                              DashSpecAstNode.GridRowDeclaration
                                  { Id = ctx.NodeId
                                    Span = ctx.Span
                                    Kind = kind
                                    TypedCells = typedCells
                                    Cells = cells
                                    Tokens = ctx.SyntaxTokens })
                      | None -> None
                  | _ -> None }

        // 4. Pipe-table line grammar (`| col1 | col2 |`).
        registerLineGrammar
            { Id = "table.pipe_row"
              TryParse = fun ctx ->
                  if ctx.RawTokens |> List.exists (fun t -> t.Kind = TokenKind.TableRow) then
                      match tryParseTableRowLine ctx.RawTokens with
                      | Some cells ->
                          let rowKind = classifyTableRow ctx.ContainerOpener ctx.ExistingTableHeader cells
                          Some(
                              DashSpecAstNode.TableRowDeclaration
                                  { Id = ctx.NodeId
                                    Span = ctx.Span
                                    Kind = rowKind
                                    Cells = cells
                                    Tokens = ctx.SyntaxTokens })
                      | None -> None
                  else
                      None }

        // 5. Flow arrow line grammar (`producer [out] -> [in] consumer`).
        registerLineGrammar
            { Id = "flow.arrow_link"
              TryParse = fun ctx ->
                  if ctx.RawTokens |> List.exists (fun t -> t.Kind = TokenKind.FlowArrow) then
                      match tryParseFlowLinkLine ctx.RawTokens with
                      | Some(fromNode, fromPort, toNode, toPort) ->
                          let sourceEndpoint = FlowEndpointSyntax.create fromNode fromPort
                          let targetEndpoint = FlowEndpointSyntax.create toNode toPort
                          Some(
                              DashSpecAstNode.FlowLinkDeclaration
                                  { Id = ctx.NodeId
                                    Span = ctx.Span
                                    Source = sourceEndpoint
                                    Target = targetEndpoint
                                    FromPath = sourceEndpoint.Path
                                    FromNode = fromNode
                                    FromPort = fromPort
                                    ToPath = targetEndpoint.Path
                                    ToNode = toNode
                                    ToPort = toPort
                                    Tokens = ctx.SyntaxTokens })
                      | None -> None
                  else
                      None }

        // 6. Clause directive line grammar (`namespace`, `scope`, `use`, `from`, `default input|output`).
        registerLineGrammar
            { Id = "clause.directive"
              TryParse = fun ctx ->
                  match ctx.ContainerOpener with
                  | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Type)
                  | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Type, _))
                  | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Ports)
                  | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Ports, _)) -> None
                  | _ ->
                      match tryParseClauseLine ctx.RawTokens with
                      | Some kind ->
                          Some(
                              DashSpecAstNode.ClauseDeclaration
                                  { Id = ctx.NodeId
                                    Span = ctx.Span
                                    Kind = kind
                                    Tokens = ctx.SyntaxTokens })
                      | None -> None }

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
            | Some _ -> false, false // bodyless source (`source id: Type = View(...)` or `UnionAll(...)`)
            | None -> true, false
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
