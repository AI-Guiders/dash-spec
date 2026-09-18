namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter

module DashSpecCodeCenterSession =

    let createDocumentSession (documentId: string) (text: string) =
        DocumentSession.createWithProviders
            documentId
            text
            DashSpecDocumentGraph.rebuildFromText
            DashSpecCompletion.getCompletions
            DashSpecCompletion.getStructuralCompletions
