namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

/// <summary>Apply <c>placement flow</c> (filter → report / page / card) — ADR-0096.</summary>
module ReportScopePlacementApplicator =

    let private placementLinks (sections: FlowGraphSections) =
        FlowGraphSections.linksFor sections FlowGraphKind.Placement

    let private filterName (link: FlowLinkDef) =
        if link.FromNode.Contains('.') then
            raise (
                DashSpecParseException(
                    $"Placement flow producer '{link.FromNode}' must be a filter id, not a dotted path."
                )
            )

        link.FromNode

    let private applyLink (contextLabel: string) (shell: ReportCompileContext) (link: FlowLinkDef) =
        let name = filterName link
        let target = FilterPlacementTargets.expectPlacementTarget contextLabel link.ToNode

        match target with
        | FlowPlacementAnchors.PlacementAnchor.Report ->
            ReportScopeFlowApplicator.applyToolbarToDashboard shell name
        | FlowPlacementAnchors.PlacementAnchor.Page pageId ->
            ReportScopeFlowApplicator.applyToolbarToPage shell.Pages pageId name
        | FlowPlacementAnchors.PlacementAnchor.Card cardId ->
            ReportScopeFlowApplicator.applyCardToolbarFilter shell.Cards cardId name

    let private applySections (contextLabel: string) (shell: ReportCompileContext) (sections: FlowGraphSections) =
        for link in placementLinks sections do
            applyLink contextLabel shell link

    let apply (shell: ReportCompileContext) =
        match shell.ReportScopeFlow with
        | Some flow -> applySections "Report" shell flow.Sections
        | None -> ()

        for page in shell.Pages |> Seq.toList do
            match page.ScopeFlow with
            | Some flow -> applySections $"Page '{page.Id}'" shell flow.Sections
            | None -> ()

        for card in shell.Cards |> Seq.toList do
            match card.InteriorFlow with
            | Some interior -> applySections $"Card '{card.Id}'" shell interior.Sections
            | None -> ()
