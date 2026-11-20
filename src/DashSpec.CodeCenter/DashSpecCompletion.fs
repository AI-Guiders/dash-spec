namespace DashSpec.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Syntax

/// CodeCenter completion provider backed directly by <see cref="DashSpecGrammarGraph"/> (DASHSPEC-ADR-0134).
module DashSpecCompletion =

    let private mapExpectation (exp: DashSpecGrammarGraph.GrammarExpectation) : CompletionItem list =
        match exp with
        | DashSpecGrammarGraph.GrammarExpectation.Keyword(word, _) ->
            [ { Label = word; Kind = "keyword"; Edit = None } ]
        | DashSpecGrammarGraph.GrammarExpectation.PropertyKey(spec, _) ->
            [ { Label = spec.Name; Kind = "property"; Edit = None } ]
        | DashSpecGrammarGraph.GrammarExpectation.ClosingKeyword word ->
            [ { Label = "end"; Kind = "keyword"; Edit = None }
              { Label = word; Kind = "keyword"; Edit = None } ]
        | DashSpecGrammarGraph.GrammarExpectation.UnitKindKeyword ->
            ImportKindRegistry.unitManifest
            |> Array.map (fun e -> { Label = e.UnitKeyword; Kind = "keyword"; Edit = None })
            |> Array.toList
        | DashSpecGrammarGraph.GrammarExpectation.ImportKindKeyword ->
            ImportKindRegistry.allDefinitions
            |> Array.map (fun d -> { Label = d.Keyword; Kind = "keyword"; Edit = None })
            |> Array.toList
        | DashSpecGrammarGraph.GrammarExpectation.TransformPluginId ->
            TransformDeclarations.registered ()
            |> List.map (fun t -> { Label = t.Id; Kind = "transform"; Edit = None })
        | DashSpecGrammarGraph.GrammarExpectation.DiagramKindId ->
            DiagramKindRegistry.knownKinds ()
            |> List.map (fun k -> { Label = k; Kind = "diagram"; Edit = None })
        | DashSpecGrammarGraph.GrammarExpectation.ValueDomain(_, values) ->
            values |> List.map (fun v -> { Label = v; Kind = "literal"; Edit = None })
        | DashSpecGrammarGraph.GrammarExpectation.SymbolRef _ ->
            []

    let getCompletions (snapshot: DocumentSnapshot) (anchor: SessionAnchor) : CompletionItem list =
        let tree = SyntaxTree.parse snapshot.Text
        let query = DashSpecGrammarGraph.expectedAt tree anchor.Offset
        query.Expectations
        |> List.collect mapExpectation
        |> List.filter (fun item ->
            System.String.IsNullOrEmpty query.Prefix
            || item.Label.StartsWith(query.Prefix, System.StringComparison.OrdinalIgnoreCase))

    let getStructuralCompletions (snapshot: DocumentSnapshot) (anchor: SessionAnchor) : StructuralCompletionItem list =
        match DocumentGraph.findNodeAt snapshot anchor.Offset with
        | None -> []
        | Some node ->
            let tree = SyntaxTree.parse snapshot.Text
            let query = DashSpecGrammarGraph.expectedAt tree anchor.Offset
            query.Expectations
            |> List.choose (function
                | DashSpecGrammarGraph.GrammarExpectation.Keyword(word, detail) ->
                    Some
                        { Label = word
                          Description = detail
                          Edit = InsertBlock(node.Id, word) }
                | _ -> None)
