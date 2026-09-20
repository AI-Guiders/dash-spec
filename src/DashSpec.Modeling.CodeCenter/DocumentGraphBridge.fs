namespace DashSpec.Modeling.CodeCenter

open System
open System.Collections.Generic
open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity
open AIGuiders.Platform.Modeling.LanguageIntelligence.Relations
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

module DashSpecDocumentGraph =

    type private OutlineRegion =
        { Label: string
          Kind: GraphNodeKind
          Start: int
          End: int
          ParentIndex: int option }

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

    let private regionEnd (node: DashSpecAstNode) =
        let mutable endPos = (DashSpecAst.span node).End

        for child in DashSpecAst.members node do
            match child with
            | DashSpecAstNode.EndBlock endBlock -> endPos <- endBlock.Span.End
            | _ -> ()

        endPos

    let private collectOutlineRegions (tree: ParseTree) =
        let regions = ResizeArray<OutlineRegion>()
        let stack = Stack<int>()

        let rec walk (node: DashSpecAstNode) =
            if DashSpecAst.isOutlineNode node then
                let parentIndex =
                    if stack.Count = 0 then None
                    else Some(stack.Peek())

                let kind =
                    match node with
                    | DashSpecAstNode.ModuleDeclaration _ -> GraphNodeKind.Module
                    | _ -> GraphNodeKind.Block

                let index = regions.Count

                regions.Add(
                    { Label = DashSpecAst.outlineLabel node
                      Kind = kind
                      Start = (DashSpecAst.span node).Start
                      End = regionEnd node
                      ParentIndex = parentIndex })

                stack.Push index

                for child in DashSpecAst.members node do
                    if not (DashSpecAst.isEndBlock child) then
                        walk child

                stack.Pop() |> ignore
            else
                for child in DashSpecAst.members node do
                    walk child

        walk tree.Root
        regions |> Seq.toList

    let private regionsToNodes (regions: OutlineRegion list) =
        let mutable counter = 1L
        let idByIndex = Array.zeroCreate regions.Length

        for i in 0 .. regions.Length - 1 do
            idByIndex.[i] <- NodeId.mint (NumericId.ofCounter counter)
            counter <- counter + 1L

        regions
        |> List.mapi (fun index region ->
            let parent =
                match region.ParentIndex with
                | None -> None
                | Some parentIndex -> Some idByIndex.[parentIndex]

            idByIndex.[index],
            ({ Id = idByIndex.[index]
               Kind = region.Kind
               Name = region.Label
               Start = region.Start
               End = region.End
               Parent = parent }
             : DocumentNode))
        |> List.map (fun (id, node) -> id, node)
        |> Map.ofList

    let private collectFoldingRegions (regions: OutlineRegion list) =
        regions
        |> List.choose (fun region ->
            if region.End <= region.Start then
                None
            else
                Some
                    ({ Range = LineRange.create region.Start region.End
                       Name = region.Label }
                     : FoldingRegion))

    let rebuildFromText (text: string) : DocumentSnapshot =
        try
            let tree = SyntaxTree.parse text
            let regions = collectOutlineRegions tree
            let nodes = regionsToNodes regions

            { Text = text
              Nodes = nodes
              TokenSpans = tokenSpans tree
              FoldingRegions = collectFoldingRegions regions }
        with :? DashSpecParseException ->
            { Text = text
              Nodes = Map.empty
              TokenSpans = []
              FoldingRegions = [] }
