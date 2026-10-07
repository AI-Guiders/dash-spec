namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core

/// <summary>
/// SSOT for document compile after token parse (ADR-0095 / ADR-0096 / ADR-0097).
/// Parsers call <see cref="finalizeShell"/> then build <see cref="DashboardDocument"/>; entry points finish via <see cref="completeDocument"/>.
/// </summary>
module DocumentCompilePipeline =

    /// <summary>Report/page/card qualified <c>{kind} flow</c> blocks applied to <see cref="DashboardShellContext"/>.</summary>
    module ScopeFlows =

        [<RequireQualifiedAccess>]
        type Phase =
            | ScopeDataFlow
            | ScopePlacementFlow
            | ScopeRouteFlow

        type PhaseDefinition =
            { Phase: Phase
              Keyword: string
              Apply: DashboardShellContext -> unit }

        let private phaseDefinitions: PhaseDefinition[] =
            [|
                { Phase = Phase.ScopeDataFlow
                  Keyword = "data"
                  Apply = ReportScopeDataFlowApplicator.apply }
                { Phase = Phase.ScopePlacementFlow
                  Keyword = "placement"
                  Apply = ReportScopePlacementApplicator.apply }
                { Phase = Phase.ScopeRouteFlow
                  Keyword = "route"
                  Apply = ReportScopeFlowApplicator.apply }
            |]

        let definitions = phaseDefinitions

        let allPhases = phaseDefinitions |> Array.map (fun d -> d.Phase)

        let apply (shell: DashboardShellContext) =
            if isNull (box shell) then
                invalidArg "shell" "Dashboard shell is required."

            for step in phaseDefinitions do
                step.Apply shell

    [<RequireQualifiedAccess>]
    type DocumentPhase =
        | MaterializeDashflow
        | BuildWiringGraph
        | ValidateDocument

    type DocumentPhaseDefinition =
        { Phase: DocumentPhase
          Keyword: string
          Transform: DashboardDocument -> DashboardDocument }

    let private documentPhaseDefinitions: DocumentPhaseDefinition[] =
        [|
            { Phase = DocumentPhase.MaterializeDashflow
              Keyword = "materialize"
              Transform = DocumentFlowMaterializer.materialize }
            { Phase = DocumentPhase.BuildWiringGraph
              Keyword = "wiring_graph"
              Transform =
                fun document ->
                    { document with WiringGraph = DocumentWiringGraphBuilder.build document } }
            { Phase = DocumentPhase.ValidateDocument
              Keyword = "validate"
              Transform =
                fun document ->
                    DashboardValidator.validate document
                    document }
        |]

    let documentPhases = documentPhaseDefinitions |> Array.map (fun d -> d.Phase)

    let runDocumentPhases (document: DashboardDocument) (phases: DocumentPhaseDefinition[]) =
        phases |> Array.fold (fun doc step -> step.Transform doc) document

    let runAllDocumentPhases document = runDocumentPhases document documentPhaseDefinitions

    let private graphAndValidatePhases =
        documentPhaseDefinitions
        |> Array.filter (fun d ->
            d.Phase = DocumentPhase.BuildWiringGraph || d.Phase = DocumentPhase.ValidateDocument)

    /// <summary>Post-parse: qualified flows → card/shell fields.</summary>
    let finalizeShell (shell: DashboardShellContext) = ScopeFlows.apply shell

    let private phase (p: DocumentPhase) =
        documentPhaseDefinitions |> Array.find (fun d -> d.Phase = p)

    let attachWiringGraph (document: DashboardDocument) =
        runDocumentPhases document [| phase DocumentPhase.BuildWiringGraph |]

    /// <summary>Wiring graph + validation without dashflow materialize (parse-time finish).</summary>
    let attachWiringGraphAndValidate (document: DashboardDocument) =
        runDocumentPhases document graphAndValidatePhases

    /// <summary>Materialize module datasources, rebuild graph, validate.</summary>
    let completeDocument (document: DashboardDocument) = runAllDocumentPhases document
