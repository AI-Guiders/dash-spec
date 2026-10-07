namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core

/// <summary>
/// SSOT for document compile phases after token parse (ADR-0095 / ADR-0096).
/// Parsers call <see cref="finalizeShell"/>; <see cref="DashboardComposer"/> calls <see cref="completeDocument"/>.
/// </summary>
module DocumentCompilePipeline =

    [<RequireQualifiedAccess>]
    type CompilePhase =
        | FinalizeShellWiring
        | MaterializeDashflow
        | BuildWiringGraph
        | ValidateDocument

    /// <summary>Post-parse: qualified flows → card/shell fields (order in <see cref="DashboardShellWiringPipeline"/>).</summary>
    let finalizeShell (shell: DashboardShellContext) =
        DashboardShellWiringPipeline.apply shell

    let attachWiringGraph (document: DashboardDocument) =
        { document with WiringGraph = DocumentWiringGraphBuilder.build document }

    let materializeDashflow (document: DashboardDocument) =
        DocumentFlowMaterializer.materialize document

    /// <summary>Materialize module datasources, rebuild graph, validate.</summary>
    let completeDocument (document: DashboardDocument) =
        let materialized = materializeDashflow document
        let withGraph = attachWiringGraph materialized
        DashboardValidator.validate withGraph
        withGraph
