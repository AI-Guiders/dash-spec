namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity
open AIGuiders.Platform.Modeling.LanguageIntelligence.Relations
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// Rebuild federation snapshot from AST + planet tiers.
module DashSpecProfileRebuild =

    let private syntaxKindToString (kind: DashSpecSyntaxKind) = kind.ToString()

    let private tokenSpans (tree: ParseTree) (graph: DashSpecConceptGraph) =
        let nodeIdAt offset =
            graph.Tiers
            |> Map.toList
            |> List.tryFind (fun (_, tier) ->
                tier.Span.Start <= offset && offset < tier.Span.End)
            |> Option.map (fun (_, tier) -> tier.Id)

        SyntaxTree.classifiedSpans tree
        |> Seq.map (fun (span: DashSpecSyntaxSpan) ->
            ({ Start = span.Start
               Length = max 1 span.Length
               Kind = syntaxKindToString span.Kind
               NodeId = nodeIdAt span.Start }
             : SessionClassificationSpan))
        |> List.ofSeq

    let private federationNodes (graph: DashSpecConceptGraph) =
        graph.Tiers
        |> Map.toList
        |> List.sortBy (fun (_, tier) -> tier.Span.Start)
        |> List.map (fun (_, tier) ->
            let parent =
                graph.Edges
                |> List.tryFind (fun edge -> edge.ChildId = tier.Id)
                |> Option.map (fun edge -> edge.ParentId)

            tier.Id,
            ({ Id = tier.Id
               Name = DashSpecConceptOntology.treeCaption tier
               Start = tier.Span.Start
               End = tier.Span.End
               Parent = parent }
             : DocumentNode))

    let private foldingRegions (graph: DashSpecConceptGraph) =
        graph.Tiers
        |> Map.toList
        |> List.choose (fun (_, tier) ->
            if tier.Span.End <= tier.Span.Start then
                None
            else
                Some
                    ({ Range = LineRange.create tier.Span.Start tier.Span.End
                       Name = DashSpecConceptOntology.treeCaption tier }
                     : FoldingRegion))

    let rebuild (text: string) : DocumentSnapshot * DashSpecConceptGraph * ProfileLawDiagnostic list =
        try
            let graph, diagnostics = DashSpecSerializeRules.parseAndBuild text

            let nodes =
                federationNodes graph
                |> List.map (fun (id, node) -> id, node)
                |> Map.ofList

            let snapshot =
                { Text = text
                  Nodes = nodes
                  TokenSpans = tokenSpans graph.Tree graph
                  FoldingRegions = foldingRegions graph }

            snapshot, graph, diagnostics
        with :? DashSpecParseException ->
            let emptyId = NodeId.mint (NumericId.ofCounter 0L)

            let emptyGraph =
                { Tree = { Text = text; Root = DashSpecAstNode.CompilationUnit { Id = emptyId; Span = TextSpan.Create 0 0; Members = Array.empty }; Tokens = Array.empty }
                  RootId = emptyId
                  Tiers = Map.empty
                  Edges = [] }

            let snapshot =
                { Text = text
                  Nodes = Map.empty
                  TokenSpans = []
                  FoldingRegions = [] }

            snapshot, emptyGraph, []
