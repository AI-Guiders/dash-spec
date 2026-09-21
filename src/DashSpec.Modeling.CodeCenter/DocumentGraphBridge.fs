namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter

module DashSpecDocumentGraph =

    let rebuildFromText (text: string) : DocumentSnapshot =
        let snapshot, _, _ = DashSpecProfileRebuild.rebuild text
        snapshot

    let rebuildWithConceptGraph (text: string) =
        DashSpecProfileRebuild.rebuild text
