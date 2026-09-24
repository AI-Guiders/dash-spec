namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.Core.Identity
open DashSpec.Modeling.Parse.Formatting
open DashSpec.Modeling.Parse.Syntax

/// Text serialization policy for DashSpec block surface (ADR-0067 SerializeRules).
module DashSpecSerializeRules =

    let defaultFormatOptions = DashSpecFormatOptions.defaultOptions

    /// Preserve source text (parse does not rewrite); used for structural round-trip checks.
    let serializePreserve (graph: DashSpecConceptGraph) = graph.Tree.Text

    /// Canonical layout projection respecting block indent laws.
    let serializeFormat (graph: DashSpecConceptGraph) =
        DashSpecBlockFormatter.format graph.Tree.Text defaultFormatOptions

    let parseAndBuild (text: string) =
        let tree = SyntaxTree.parse text
        let graph = DashSpecConceptGraphBuilder.build tree
        graph, DashSpecInvariantLaws.all graph

    /// Structural round-trip: parse → concept graph → preserve serialize → parse yields equivalent outline.
    let roundTripOutline (text: string) =
        let graph, laws = parseAndBuild text

        if not (List.isEmpty laws) then
            Error laws
        else
            let reparsed = parseAndBuild (serializePreserve graph)

            let outlineSignature (g: DashSpecConceptGraph) =
                g.Tiers
                |> Map.toList
                |> List.sortBy (fun (_, tier) -> tier.Span.Start)
                |> List.map (fun (_, tier) ->
                    let id = NumericId.value (NodeId.carrier tier.Id)
                    $"{id}:{DashSpecConceptOntology.outlineCaption tier.Kind}:{tier.ProjectionRole}")

            if outlineSignature graph = outlineSignature (fst reparsed) then
                Ok graph
            else
                Error
                    [ DashSpecRuleRegistry.diagnostic
                          DashSpecRuleKind.RoundTripOutlineChanged
                          (TextSpan.Create 0 1) ]
