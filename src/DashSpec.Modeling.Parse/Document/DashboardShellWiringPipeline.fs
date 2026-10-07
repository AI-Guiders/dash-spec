namespace DashSpec.Modeling.Parse.Document

/// <summary>
/// Back-compat alias; shell wiring registry lives in <see cref="DocumentCompilePipeline.ShellWiring"/> (ADR-0097).
/// </summary>
module DashboardShellWiringPipeline =

    type WiringPhase = DocumentCompilePipeline.ShellWiring.Phase
    type WiringPhaseDefinition = DocumentCompilePipeline.ShellWiring.PhaseDefinition

    let allPhases = DocumentCompilePipeline.ShellWiring.allPhases
    let definitions = DocumentCompilePipeline.ShellWiring.definitions
    let apply shell = DocumentCompilePipeline.ShellWiring.apply shell
