namespace DashSpec.CodeCenter

open DashSpec.Modeling.Core

/// ADR-0067 InvariantLaws entry point — delegates to graph rule engine.
module DashSpecInvariantLaws =

    let all = DashSpecRuleEngine.evaluate

    let balancedBlockStructure = DashSpecRuleEngine.evaluateBlockBalance

    let outlineSpansAreValid (graph: DashSpecConceptGraph) =
        DashSpecRuleEngine.evaluate graph
        |> List.filter (fun diagnostic -> diagnostic.Code = DashSpecRules.code DashSpecRule.EmptyOutlineSpan)

    let cardReferencesUnderCards (graph: DashSpecConceptGraph) =
        DashSpecRuleEngine.evaluate graph
        |> List.filter (fun diagnostic -> diagnostic.Code = DashSpecRules.code DashSpecRule.CardReferenceOutsideCards)
