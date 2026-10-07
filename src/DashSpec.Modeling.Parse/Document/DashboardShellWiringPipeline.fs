namespace DashSpec.Modeling.Parse.Document

/// <summary>
/// Back-compat alias; scope-flow registry lives in <see cref="DocumentCompilePipeline.ScopeFlows"/> (ADR-0097).
/// </summary>
module DashboardShellWiringPipeline =

    type WiringPhase = DocumentCompilePipeline.ScopeFlows.Phase
    type WiringPhaseDefinition = DocumentCompilePipeline.ScopeFlows.PhaseDefinition

    let allPhases = DocumentCompilePipeline.ScopeFlows.allPhases
    let definitions = DocumentCompilePipeline.ScopeFlows.definitions
    let apply shell = DocumentCompilePipeline.ScopeFlows.apply shell
