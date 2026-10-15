namespace DashSpec.CodeCenter

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// <summary>CodeCenter rule evaluation: language rules come from DashSpec; graph-only rules stay here.</summary>
module DashSpecRuleEngine =

    let rec private outlineNodes (node: DashSpecAstNode) : DashSpecAstNode list =
        [ if DashSpecAst.isOutlineNode node then
              yield node

          for child in DashSpecAst.members node do
              yield! outlineNodes child ]

    /// Predicate: concept outline span is empty in the built graph.
    let private emptyOutlineFindings (graph: DashSpecConceptGraph) : DashSpecRuleFinding list =
        outlineNodes graph.Tree.Root
        |> List.choose (fun node ->
            match Map.tryFind (DashSpecAst.id node) graph.Tiers with
            | Some concept when concept.Span.End <= concept.Span.Start ->
                let finding: DashSpecRuleFinding =
                    { Rule = DashSpecRule.EmptyOutlineSpan
                      Message = $"outline node '{DashSpecConceptOntology.outlineCaption concept.Kind}' has empty span"
                      Span = concept.Span }

                Some finding
            | _ -> None)

    let evaluateFindings (graph: DashSpecConceptGraph) : DashSpecRuleFinding list =
        DashSpecRuleChecks.findings graph.Tree.Root @ emptyOutlineFindings graph

    let evaluate (graph: DashSpecConceptGraph) : ProfileLawDiagnostic list =
        evaluateFindings graph |> List.map DashSpecDiagnosticCatalog.ofFinding

    let evaluateBlockBalance (graph: DashSpecConceptGraph) : ProfileLawDiagnostic list =
        evaluateFindings graph
        |> List.filter (fun finding -> DashSpecRules.isBlockBalance finding.Rule)
        |> List.map DashSpecDiagnosticCatalog.ofFinding
