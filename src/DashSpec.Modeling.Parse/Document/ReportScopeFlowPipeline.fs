namespace DashSpec.Modeling.Parse.Document

/// <summary>
/// Facade over <see cref="DocumentCompilePipeline.ScopeFlows"/> for ADR-0095 cross-references.
/// </summary>
module ReportScopeFlowPipeline =

    type ScopeFlowPhase = DocumentCompilePipeline.ScopeFlows.Phase
    type ScopeFlowPhaseDefinition = DocumentCompilePipeline.ScopeFlows.PhaseDefinition

    let allPhases = DocumentCompilePipeline.ScopeFlows.allPhases
    let definitions = DocumentCompilePipeline.ScopeFlows.definitions
    let apply ctx = DocumentCompilePipeline.ScopeFlows.apply ctx
