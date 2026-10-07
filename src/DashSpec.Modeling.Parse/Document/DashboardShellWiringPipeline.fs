namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core

/// <summary>
/// SSOT for post-parse report shell wiring (ADR-0095). Call once after report/cards/pages are parsed;
/// order is defined here only — not in DocumentModuleParser / TabModuleParser.
/// </summary>
module DashboardShellWiringPipeline =

    /// <summary>Ordered wiring phases; extend the registry — do not add ad-hoc applicator calls elsewhere.</summary>
    [<RequireQualifiedAccess>]
    type WiringPhase =
        | ScopeDataFlow
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
