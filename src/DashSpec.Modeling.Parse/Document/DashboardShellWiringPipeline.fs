namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core

/// <summary>
/// SSOT for post-parse report shell wiring (ADR-0095). Call via <see cref="DocumentCompilePipeline.finalizeShell"/>.
/// </summary>
module DashboardShellWiringPipeline =

    [<RequireQualifiedAccess>]
    type WiringPhase =
        | ScopeDataFlow
        | ScopePlacementFlow
        | ScopeRouteFlow

    type WiringPhaseDefinition =
        { Phase: WiringPhase
          Keyword: string
          Apply: DashboardShellContext -> unit }

    let private definitions: WiringPhaseDefinition[] =
        [|
            { Phase = WiringPhase.ScopeDataFlow
              Keyword = "data"
              Apply = ReportScopeDataFlowApplicator.apply }
            { Phase = WiringPhase.ScopePlacementFlow
              Keyword = "placement"
              Apply = ReportScopePlacementApplicator.apply }
            { Phase = WiringPhase.ScopeRouteFlow
              Keyword = "route"
              Apply = ReportScopeFlowApplicator.apply }
        |]

    let allPhases = definitions |> Array.map (fun d -> d.Phase)

    let apply (shell: DashboardShellContext) =
        if isNull (box shell) then
            invalidArg "shell" "Dashboard shell is required."

        for step in definitions do
            step.Apply shell
