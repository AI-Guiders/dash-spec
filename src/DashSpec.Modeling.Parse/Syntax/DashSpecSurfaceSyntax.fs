namespace DashSpec.Modeling.Parse.Syntax

open System
open System.Collections.Generic
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
          Id: NodeId
          Span: TextSpan
          Directive: DashSpecModuleDirective option
          Opener: DashSpecBlockOpener option
          EndKeyword: DashSpecBlockKeyword option
          EndId: string option
          CardId: string option
          Tokens: SyntaxToken[]
          Children: ResizeArray<MutableNode> }

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

    let private appendBlank (parent: MutableNode) (id: int ref) (line: TokenReaderPhysicalLines.PhysicalLine) =
        parent.Children.Add
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

    let private appendLine (parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) =
        parent.Children.Add
            { Kind = DashSpecAstNodeKind.Line
              Id = nextId id
              Span = line.Span
              Directive = None
              Opener = None
              EndKeyword = None
              EndId = None
              CardId = None
              Tokens = lineSyntaxTokens text allTokens line
              Children = ResizeArray() }

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

        parent.Children.Add
            { Kind = DashSpecAstNodeKind.EndBlock
              Id = nextId id
              Span = line.Span
              Directive = None
              Opener = None
              EndKeyword = Some endKeyword
              EndId = endId
              CardId = None
              Tokens = lineSyntaxTokens text allTokens line
              Children = ResizeArray() }

    let private appendCardRef (parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (cardId: string) =
        parent.Children.Add
            { Kind = DashSpecAstNodeKind.CardReference
              Id = nextId id
              Span = line.Span
              Directive = None
              Opener = None
              EndKeyword = None
              EndId = None
              CardId = Some cardId
              Tokens = lineSyntaxTokens text allTokens line
              Children = ResizeArray() }

    let private appendModule (parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (kind: string) (moduleId: string) =
        let directive =
            if String.Equals(kind, "tab", StringComparison.OrdinalIgnoreCase) then
                DashSpecModuleDirective.Tab moduleId
            else
                DashSpecModuleDirective.Dashboard moduleId

        { Kind = DashSpecAstNodeKind.ModuleDeclaration
          Id = nextId id
          Span = line.Span
          Directive = Some directive
          Opener = None
          EndKeyword = None
          EndId = None
          CardId = None
          Tokens = lineSyntaxTokens text allTokens line
          Children = ResizeArray() }

    let private appendBlock (parent: MutableNode) (id: int ref) (text: string) (allTokens: Token[]) (line: TokenReaderPhysicalLines.PhysicalLine) (kind: string) (blockId: string option) =
        let opener =
            match kind, blockId with
            | "grid", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Grid
            | "chrome", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Chrome
            | "click", _ -> DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.OnClick
            | kind, id -> DashSpecBlockOpenerSyntax.ofLineKind(BlockSurfaceLineClassifier.BlockOpener(kind, id))

        { Kind = DashSpecAstNodeKind.BlockDeclaration
          Id = nextId id
          Span = line.Span
          Directive = None
          Opener = Some opener
          EndKeyword = None
          EndId = None
          CardId = None
          Tokens = lineSyntaxTokens text allTokens line
          Children = ResizeArray() }

    /// Parse block/module body until <c>reader</c> is exhausted or an <c>end</c> line closes <paramref name="container" />.
    let rec private parseContainerBody
        (container: MutableNode)
        (id: int ref)
        (text: string)
        (allTokens: Token[])
        (reader: TokenReader)
        (closeContainer: bool)
        =
        let mutable continueParsing = true

        while continueParsing && not reader.IsEof do
            match TokenReaderPhysicalLines.readNext reader with
            | None -> continueParsing <- false
            | Some line ->
                let lineKind = BlockSurfaceLineClassifier.classifyLineTokens line.Tokens

                match lineKind with
                | BlockSurfaceLineClassifier.Blank -> appendBlank container id line
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
                            parseContainerBody moduleNode id text allTokens reader true
                        | BlockSurfaceLineClassifier.BlockOpener(kind, blockId) ->
                            let blockNode = appendBlock container id text allTokens line kind blockId
                            container.Children.Add blockNode
                            parseContainerBody blockNode id text allTokens reader true
                        | BlockSurfaceLineClassifier.End(kind, endId) ->
                            appendEndBlock container id text allTokens line kind endId

                            if closeContainer then
                                continueParsing <- false
                        | BlockSurfaceLineClassifier.Blank -> ()
                        | BlockSurfaceLineClassifier.BraceOpen
                        | BlockSurfaceLineClassifier.BraceClose
                        | BlockSurfaceLineClassifier.Content ->
                            appendLine container id text allTokens line

    let parse (text: string) : ParseTree =
        let allTokens = DashSpecLexer.tokenize text |> Array.ofSeq
        let reader = TokenReader(allTokens, text)
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

        parseContainerBody root id text allTokens reader false

        let syntaxTokens =
            allTokens
            |> Array.choose (fun token ->
                match token.Kind with
                | TokenKind.Newline | TokenKind.Eof -> None
                | _ -> Some token)
            |> Array.mapi (fun index token ->
                SyntaxTokenClassifier.toSyntaxToken text allTokens (tokenIndex allTokens token) token)

        { Text = text
          Root = toImmutable root
          Tokens = syntaxTokens }
