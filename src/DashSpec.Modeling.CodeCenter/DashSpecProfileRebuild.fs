namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity
open AIGuiders.Platform.Modeling.LanguageIntelligence.Relations
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// Rebuild federation snapshot from DashSpec concept graph (planet SSOT pipeline).
module DashSpecProfileRebuild =

    let nodeIdFromAst (astId: uint32) : NodeId =
        NodeId.mint (NumericId.ofCounter (int64 astId))

    let private syntaxKindToString (kind: DashSpecSyntaxKind) = kind.ToString()

    let private tokenSpans (tree: ParseTree) (graph: DashSpecConceptGraph) =
        let nodeIdAt offset =
            graph.Nodes
            |> Map.toList
            |> List.tryFind (fun (_, concept) ->
                concept.Span.Start <= offset && offset < concept.Span.End)
            |> Option.map (fun (_, concept) -> nodeIdFromAst concept.AstId)

        SyntaxTree.classifiedSpans tree
        |> Seq.map (fun (span: DashSpecSyntaxSpan) ->
            ({ Start = span.Start
               Length = max 1 span.Length
               Kind = syntaxKindToString span.Kind
               NodeId = nodeIdAt span.Start }
             : SessionClassificationSpan))
        |> List.ofSeq

    let private federationNodes (graph: DashSpecConceptGraph) =
        graph.Nodes
        |> Map.toList
        |> List.sortBy (fun (_, node) -> node.Span.Start)
        |> List.map (fun (_, concept) ->
            let parent =
                graph.Edges
                |> List.tryFind (fun edge -> edge.ChildAstId = concept.AstId)
                |> Option.map (fun edge -> nodeIdFromAst edge.ParentAstId)

            nodeIdFromAst concept.AstId,
            ({ Id = nodeIdFromAst concept.AstId
               Name = concept.Label
               Start = concept.Span.Start
               End = concept.Span.End
               Parent = parent }
             : DocumentNode))

    let private foldingRegions (graph: DashSpecConceptGraph) =
        graph.Nodes
        |> Map.toList
        |> List.choose (fun (_, concept) ->
            if concept.Span.End <= concept.Span.Start then
                None
            else
                Some
                    ({ Range = LineRange.create concept.Span.Start concept.Span.End
                       Name = concept.Label }
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
            let emptyGraph =
                { Tree = { Text = text; Root = DashSpecAstNode.CompilationUnit { Id = 0u; Span = TextSpan.Create 0 0; Members = Array.empty }; Tokens = Array.empty }
                  RootAstId = 0u
                  Nodes = Map.empty
                  Edges = [] }

            let snapshot =
                { Text = text
                  Nodes = Map.empty
                  TokenSpans = []
                  FoldingRegions = [] }

            snapshot, emptyGraph, []
