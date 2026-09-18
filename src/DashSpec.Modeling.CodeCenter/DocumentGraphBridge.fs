namespace DashSpec.Modeling.CodeCenter

open System
open System.Collections.Generic
open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity
open AIGuiders.Platform.Modeling.LanguageIntelligence.Relations
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

module DashSpecDocumentGraph =

    let private syntaxKindToString (kind: DashSpecSyntaxKind) = kind.ToString()

    let private tokenSpans (tree: ParseTree) =
        SyntaxTree.classifiedSpans tree
        |> Seq.map (fun (span: DashSpecSyntaxSpan) ->
            ({ Start = span.Start
               Length = max 1 span.Length
               Kind = syntaxKindToString span.Kind
               NodeId = None }
             : SessionClassificationSpan))
        |> List.ofSeq

    let private firstIdent (node: SyntaxNode) =
        node.Tokens
        |> Array.tryFind (fun (token: SyntaxToken) -> token.Kind = DashSpecSyntaxKind.Identifier)
        |> Option.map (fun token -> token.Text)
        |> Option.defaultValue String.Empty

    let private moduleHeaderKind (node: SyntaxNode) =
        node.Tokens
        |> Array.tryFind (fun (token: SyntaxToken) -> token.Kind = DashSpecSyntaxKind.ModuleHeader)
        |> Option.map (fun token -> token.Text)
        |> Option.defaultValue "@module"

    let private blockKind (node: SyntaxNode) =
        if node.Tokens |> Array.exists (fun (token: SyntaxToken) -> token.Text = "tab") then
            "tab"
        else
            "block"

    let private endBlockName (node: SyntaxNode) =
        node.Tokens
        |> Array.tryFind (fun (token: SyntaxToken) -> token.Kind = DashSpecSyntaxKind.Identifier)
        |> Option.map (fun token -> token.Text)
        |> Option.defaultValue "end"

    let private foldName (node: SyntaxNode) =
        match node.Kind with
        | SyntaxNodeKind.ModuleDeclaration -> moduleHeaderKind node + " " + firstIdent node
        | SyntaxNodeKind.Block -> blockKind node + " " + firstIdent node
        | SyntaxNodeKind.EndBlock -> "end " + endBlockName node
        | _ -> node.Kind.ToString()

    let private collectFoldingRegions (tree: ParseTree) =
        let regions = ResizeArray<FoldingRegion>()

        let rec walk (node: SyntaxNode) =
            match node.Kind with
            | SyntaxNodeKind.ModuleDeclaration
            | SyntaxNodeKind.Block when node.Children.Length > 0 && node.Span.Length > 1 ->
                regions.Add(
                    ({ Range = LineRange.create node.Span.Start node.Span.End
                       Name = foldName node }
                     : FoldingRegion))
            | _ -> ()

            for child in node.Children do
                walk child

        walk tree.Root
        regions |> Seq.toList

    let private collectNodes (tree: ParseTree) =
        let mutable counter = 1L
        let mutable nodes = Map.empty
        let mutable dashboardParent: NodeId option = None

        let mintId () =
            let id = NodeId.mint (NumericId.ofCounter counter)
            counter <- counter + 1L
            id

        let rec walk (node: SyntaxNode) (parent: NodeId option) =
            match node.Kind with
            | SyntaxNodeKind.ModuleDeclaration ->
                let id = mintId()

                let docNode =
                    ({ Id = id
                       Kind = moduleHeaderKind node
                       Name = firstIdent node
                       Start = node.Span.Start
                       End = node.Span.End
                       Parent = None }
                     : DocumentNode)

                nodes <- nodes |> Map.add id docNode
                dashboardParent <- Some id

                for child in node.Children do
                    walk child (Some id)
            | SyntaxNodeKind.Block ->
                let id = mintId()

                let docNode =
                    ({ Id = id
                       Kind = blockKind node
                       Name = firstIdent node
                       Start = node.Span.Start
                       End = node.Span.End
                       Parent = parent |> Option.orElse dashboardParent }
                     : DocumentNode)

                nodes <- nodes |> Map.add id docNode

                for child in node.Children do
                    walk child (Some id)
            | SyntaxNodeKind.EndBlock ->
                let id = mintId()

                let docNode =
                    ({ Id = id
                       Kind = "end"
                       Name = endBlockName node
                       Start = node.Span.Start
                       End = node.Span.End
                       Parent = parent |> Option.orElse dashboardParent }
                     : DocumentNode)

                nodes <- nodes |> Map.add id docNode

                for child in node.Children do
                    walk child parent
            | _ ->
                for child in node.Children do
                    walk child parent

        walk tree.Root None
        nodes

    let rebuildFromText (text: string) : DocumentSnapshot =
        try
            let tree = SyntaxTree.parse text

            { Text = text
              Nodes = collectNodes tree
              TokenSpans = tokenSpans tree
              FoldingRegions = collectFoldingRegions tree }
        with :? DashSpecParseException ->
            { Text = text
              Nodes = Map.empty
              TokenSpans = []
              FoldingRegions = [] }
