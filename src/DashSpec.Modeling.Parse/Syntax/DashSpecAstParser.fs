namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Formatting
open DashSpec.Modeling.Parse.Lexing

module DashSpecBlockOpenerSyntax =

    let ofLineKind (lineKind: BlockFormatterRules.LineKind) =
        match lineKind with
        | BlockFormatterRules.BlockOpener(kind, id) ->
            match DashSpecBlockKeyword.tryOfName kind, id with
            | Some keyword, Some identifier -> DashSpecBlockOpener.Named(keyword, identifier)
            | Some keyword, None -> DashSpecBlockOpener.Anonymous keyword
            | None, Some identifier -> DashSpecBlockOpener.Named(DashSpecBlockKeyword.Other kind, identifier)
            | None, None -> DashSpecBlockOpener.Anonymous(DashSpecBlockKeyword.Other kind)
        | _ -> failwith "not a block opener line"

module DashSpecAstParser =

    type private LineSegment =
        { Span: TextSpan
          Trimmed: string
          Tokens: Token list }

    type private DashSpecAstNodeKind =
        | CompilationUnit
        | ModuleDeclaration
        | BlockDeclaration
        | CardReference
        | EndBlock
        | Line
        | BlankLine

    type private MutableNode =
        { Kind: DashSpecAstNodeKind
          Id: AstNodeId
          Span: TextSpan
          Directive: DashSpecModuleDirective option
          Opener: DashSpecBlockOpener option
          EndKeyword: DashSpecBlockKeyword option
          EndId: string option
          CardId: string option
          Tokens: SyntaxToken[]
          Children: ResizeArray<MutableNode> }

    let private nextId (counter: int ref) =
        let id = AstNodeId.create (uint32 counter.Value)
        counter.Value <- counter.Value + 1
        id

    let private splitLines (source: string) (tokens: Token[]) =
        let lines = ResizeArray<LineSegment>()
        let current = ResizeArray<Token>()
        let mutable lineStart = 0

        let flush () =
            if current.Count = 0 then
                lines.Add({ Span = TextSpan.Create lineStart 0; Trimmed = String.Empty; Tokens = [] })
            else
                let list = current |> Seq.toList
                let start = list |> List.head |> fun t -> t.Start
                let last = List.last list
                let length = last.Start + max 1 last.Length - start
                let trimmed = source.Substring(start, length).Trim()
                lines.Add({ Span = TextSpan.Create start length; Trimmed = trimmed; Tokens = list })

            current.Clear()

        for token in tokens do
            match token.Kind with
            | TokenKind.Eof -> ()
            | TokenKind.Newline ->
                flush ()
                lineStart <- token.Start + max 1 token.Length
            | _ ->
                if current.Count = 0 then lineStart <- token.Start
                current.Add token

        flush ()
        lines :> IReadOnlyList<_>

    let private tokenIndex (allTokens: Token[]) (token: Token) =
        allTokens |> Array.findIndex (fun t -> t.Start = token.Start && t.Kind = token.Kind)

    let private lineSyntaxTokens (source: string) (allTokens: Token[]) (line: LineSegment) =
        line.Tokens
        |> List.map (fun token ->
            SyntaxTokenClassifier.toSyntaxToken source allTokens (tokenIndex allTokens token) token)
        |> Array.ofList

    let private isCardsParent (frame: MutableNode) =
        match frame.Opener with
        | Some(DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Cards)
        | Some(DashSpecBlockOpener.Named(DashSpecBlockKeyword.Cards, _)) -> true
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

        match node.Kind with
        | CompilationUnit ->
            DashSpecAstNode.CompilationUnit { Id = node.Id; Span = span; Members = children }
        | ModuleDeclaration ->
            DashSpecAstNode.ModuleDeclaration
                { Id = node.Id
                  Span = span
                  Directive = node.Directive.Value
                  HeaderSpan = node.Span
                  Tokens = node.Tokens
                  Members = children }
        | BlockDeclaration ->
            DashSpecAstNode.BlockDeclaration
                { Id = node.Id
                  Span = span
                  Opener = node.Opener.Value
                  HeaderSpan = node.Span
                  Tokens = node.Tokens
                  Members = children }
        | CardReference ->
            DashSpecAstNode.CardReference
                { Id = node.Id
                  Span = span
                  CardId = node.CardId.Value
                  Tokens = node.Tokens }
        | EndBlock ->
            DashSpecAstNode.EndBlock
                { Id = node.Id
                  Span = span
                  EndKeyword = node.EndKeyword.Value
                  EndId = node.EndId
                  Tokens = node.Tokens }
        | Line -> DashSpecAstNode.Line { Id = node.Id; Span = span; Tokens = node.Tokens }
        | BlankLine -> DashSpecAstNode.BlankLine { Id = node.Id; Span = span }

    let parse (text: string) : ParseTree =
        let tokens = DashSpecLexer.tokenize text |> Array.ofSeq
        let lines = splitLines text tokens
        let id = ref 1

        let root =
            { Kind = DashSpecAstNodeKind.CompilationUnit
              Id = nextId id
              Span = TextSpan.Create 0 (max 0 text.Length)
              Directive = None
              Opener = None
              EndKeyword = None
              EndId = None
              CardId = None
              Tokens = Array.empty
              Children = ResizeArray() }

        let stack = Stack<MutableNode>()
        stack.Push root

        let appendChild node =
            stack.Peek().Children.Add node

        for line in lines do
            if String.IsNullOrWhiteSpace line.Trimmed then
                appendChild
                    { Kind = DashSpecAstNodeKind.BlankLine
                      Id = nextId id
                      Span = line.Span
                      Directive = None
                      Opener = None
                      EndKeyword = None
                      EndId = None
                      CardId = None
                      Tokens = Array.empty
                      Children = ResizeArray() }
            else
                let parent = stack.Peek()

                let cardRef =
                    if isCardsParent parent then
                        match line.Tokens with
                        | [ { Kind = TokenKind.Ident; Value = cardId } ] -> Some cardId
                        | _ -> None
                    else
                        None

                match cardRef with
                | Some cardId ->
                    appendChild
                        { Kind = DashSpecAstNodeKind.CardReference
                          Id = nextId id
                          Span = line.Span
                          Directive = None
                          Opener = None
                          EndKeyword = None
                          EndId = None
                          CardId = Some cardId
                          Tokens = lineSyntaxTokens text tokens line
                          Children = ResizeArray() }
                | None ->
                    match BlockFormatterRules.classifyLine line.Trimmed with
                    | BlockFormatterRules.ModuleHeader(kind, moduleId) ->
                        let directive =
                            if String.Equals(kind, "tab", StringComparison.OrdinalIgnoreCase) then
                                DashSpecModuleDirective.Tab moduleId
                            else
                                DashSpecModuleDirective.Dashboard moduleId

                        let node =
                            { Kind = DashSpecAstNodeKind.ModuleDeclaration
                              Id = nextId id
                              Span = line.Span
                              Directive = Some directive
                              Opener = None
                              EndKeyword = None
                              EndId = None
                              CardId = None
                              Tokens = lineSyntaxTokens text tokens line
                              Children = ResizeArray() }

                        appendChild node
                        stack.Push node
                    | BlockFormatterRules.BlockOpener(kind, blockId) ->
                        let opener =
                            match kind, blockId with
                            | "grid", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid
                            | "chrome", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Chrome
                            | "click", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.OnClick
                            | kind, id -> DashSpecBlockOpenerSyntax.ofLineKind(BlockFormatterRules.BlockOpener(kind, id))

                        let node =
                            { Kind = DashSpecAstNodeKind.BlockDeclaration
                              Id = nextId id
                              Span = line.Span
                              Directive = None
                              Opener = Some opener
                              EndKeyword = None
                              EndId = None
                              CardId = None
                              Tokens = lineSyntaxTokens text tokens line
                              Children = ResizeArray() }

                        appendChild node
                        stack.Push node
                    | BlockFormatterRules.End(kind, endId) ->
                        let endKeyword =
                            DashSpecBlockKeyword.tryOfName kind
                            |> Option.defaultWith (fun () -> DashSpecBlockKeyword.Other kind)

                        appendChild
                            { Kind = DashSpecAstNodeKind.EndBlock
                              Id = nextId id
                              Span = line.Span
                              Directive = None
                              Opener = None
                              EndKeyword = Some endKeyword
                              EndId = endId
                              CardId = None
                              Tokens = lineSyntaxTokens text tokens line
                              Children = ResizeArray() }

                        if stack.Count > 1 then stack.Pop() |> ignore
                    | BlockFormatterRules.Blank ->
                        appendChild
                            { Kind = DashSpecAstNodeKind.BlankLine
                              Id = nextId id
                              Span = line.Span
                              Directive = None
                              Opener = None
                              EndKeyword = None
                              EndId = None
                              CardId = None
                              Tokens = Array.empty
                              Children = ResizeArray() }
                    | BlockFormatterRules.BraceOpen
                    | BlockFormatterRules.BraceClose
                    | BlockFormatterRules.Content ->
                        appendChild
                            { Kind = DashSpecAstNodeKind.Line
                              Id = nextId id
                              Span = line.Span
                              Directive = None
                              Opener = None
                              EndKeyword = None
                              EndId = None
                              CardId = None
                              Tokens = lineSyntaxTokens text tokens line
                              Children = ResizeArray() }

        let syntaxTokens =
            tokens
            |> Array.choose (fun token ->
                match token.Kind with
                | TokenKind.Newline | TokenKind.Eof -> None
                | _ -> Some token)
            |> Array.mapi (fun index token ->
                SyntaxTokenClassifier.toSyntaxToken text tokens (tokenIndex tokens token) token)

        { Text = text
          Root = toImmutable root
          Tokens = syntaxTokens }
