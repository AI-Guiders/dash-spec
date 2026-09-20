namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter

module DashSpecCodeCenterSession =

    let createDocumentSession (documentId: string) (text: string) =
        DocumentSession.createWithProviders
            documentId
            text
            DashSpecDocumentLanguageProfile.instance
            DashSpecCompletion.getCompletions
            DashSpecCompletion.getStructuralCompletions
