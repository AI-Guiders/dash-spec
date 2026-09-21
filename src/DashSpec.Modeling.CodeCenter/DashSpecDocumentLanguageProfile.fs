namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter

/// DashSpec planet Language Profile for federation Code Center (GUIDERS-ADR-0067 slice).
type DashSpecDocumentLanguageProfile() =
    interface IDocumentLanguageProfile with
        member _.ProfileRef = { ProfileId = "dashspec.block"; Flavour = None }
        member _.Surface = SurfaceFamily.BlockText
        member _.Rebuild text = DashSpecDocumentGraph.rebuildFromText text
        member _.PlanStructural snapshot edit = StructuralPlanGraph.plan snapshot edit

        member _.AvailableProjections () = DashSpecProjectionHints.availableProjections ()

module DashSpecDocumentLanguageProfile =
    let instance : IDocumentLanguageProfile = DashSpecDocumentLanguageProfile()
