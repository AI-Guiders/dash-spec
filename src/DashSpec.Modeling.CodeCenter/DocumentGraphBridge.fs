namespace DashSpec.Modeling.CodeCenter

open System
open System.Collections.Generic
open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity
open AIGuiders.Platform.Modeling.LanguageIntelligence.Relations
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Formatting
open DashSpec.Modeling.Parse.Syntax

module DashSpecDocumentGraph =

    type private OutlineRegion =
        { Label: string
          Kind: GraphNodeKind
          Start: int
          End: int
          ParentIndex: int option }

    let private syntaxKindToString (kind: DashSpecSyntaxKind) = kind.ToString()

    let private lineText (node: SyntaxNode) =
        String.Join(" ", node.Tokens |> Array.map (fun token -> token.Text)).Trim()

    let private nodeLabel (node: SyntaxNode) =
        let trimmed = lineText node

        match BlockFormatterRules.classifyLine trimmed with
        | BlockFormatterRules.ModuleHeader(kind, id) -> $"@{kind} {id}"
        | BlockFormatterRules.BlockOpener(kind, id) ->
            match id with
            | Some id -> $"{kind} {id}"
            | None -> kind
        | BlockFormatterRules.End _ -> trimmed
        | _ -> trimmed

    let private tokenSpans (tree: ParseTree) =
        SyntaxTree.classifiedSpans tree
        |> Seq.map (fun (span: DashSpecSyntaxSpan) ->
            ({ Start = span.Start
               Length = max 1 span.Length
               Kind = syntaxKindToString span.Kind
               NodeId = None }
             : SessionClassificationSpan))
        |> List.ofSeq

    let private regionEnd (node: SyntaxNode) =
        let mutable endPos = node.Span.End

        for child in node.Children do
            if child.Kind = SyntaxNodeKind.EndBlock then
                endPos <- child.Span.End

        endPos

    let private collectOutlineRegions (tree: ParseTree) =
        let regions = ResizeArray<OutlineRegion>()
        let stack = Stack<int>()

        let rec walk (node: SyntaxNode) =
            match node.Kind with
            | SyntaxNodeKind.ModuleDeclaration ->
                let index = regions.Count

                regions.Add(
                    { Label = nodeLabel node
                      Kind = GraphNodeKind.Module
                      Start = node.Span.Start
                      End = regionEnd node
                      ParentIndex = None })

                stack.Push index

                for child in node.Children do
                    if child.Kind <> SyntaxNodeKind.EndBlock then
                        walk child

                stack.Pop() |> ignore
            | SyntaxNodeKind.Block ->
                let parentIndex =
                    if stack.Count = 0 then None
                    else Some(stack.Peek())

                let index = regions.Count

                regions.Add(
                    { Label = nodeLabel node
                      Kind = GraphNodeKind.Block
                      Start = node.Span.Start
                      End = regionEnd node
                      ParentIndex = parentIndex })

                stack.Push index

                for child in node.Children do
                    if child.Kind <> SyntaxNodeKind.EndBlock then
                        walk child

                stack.Pop() |> ignore
            | SyntaxNodeKind.EndBlock -> ()
            | _ ->
                for child in node.Children do
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

    let private findRegionIndex (regions: OutlineRegion list) (label: string) =
        regions
        |> List.tryFindIndex (fun region -> String.Equals(region.Label, label, StringComparison.OrdinalIgnoreCase))

    let private blockLabel kind id =
        match id with
        | Some id -> $"{kind} {id}"
        | None -> kind

    let private tryMergeIr (text: string) (regions: OutlineRegion list) =
        try
            let document = DocumentModuleParser.parseDocumentDefault text None
            let merged = ResizeArray<OutlineRegion>(regions)
            let moduleLabel = $"@dashboard {document.Id}"

            let ensureRegion label kind parentIndex =
                match findRegionIndex (merged |> Seq.toList) label with
                | Some _ -> ()
                | None ->
                    let start = text.IndexOf(label, StringComparison.Ordinal)

                    if start >= 0 then
                        merged.Add(
                            { Label = label
                              Kind = kind
                              Start = start
                              End = start + label.Length
                              ParentIndex = parentIndex })

            let moduleIndex =
                match findRegionIndex (merged |> Seq.toList) moduleLabel with
                | Some index -> Some index
                | None ->
                    ensureRegion moduleLabel GraphNodeKind.Module None
                    findRegionIndex (merged |> Seq.toList) moduleLabel

            for tab in document.Tabs do
                let label = blockLabel "tab" (Some tab.Id)
                ensureRegion label GraphNodeKind.Block moduleIndex

            for card in document.Cards do
                let label = blockLabel "card" (Some card.Id)

                let parentIndex =
                    document.Tabs
                    |> Seq.tryPick (fun tab ->
                        if
                            tab.CardIds
                            |> Seq.exists (fun id -> String.Equals(id, card.Id, StringComparison.OrdinalIgnoreCase))
                        then
                            findRegionIndex (merged |> Seq.toList) (blockLabel "tab" (Some tab.Id))
                        else
                            None)
                    |> Option.orElse moduleIndex

                ensureRegion label GraphNodeKind.Block parentIndex

            merged |> Seq.toList
        with
        | :? DashSpecParseException
        | :? ArgumentException -> regions

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
            let regions = collectOutlineRegions tree |> tryMergeIr text
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
