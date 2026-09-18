namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.CodeCenter

module DashSpecCompletion =

    let getCompletions (_snapshot: DocumentSnapshot) (_anchor: SessionAnchor) : CompletionItem list =
        [ { Label = "tab"; Kind = "structural"; Edit = None }
          { Label = "dashboard"; Kind = "literal"; Edit = None } ]

    let getStructuralCompletions (snapshot: DocumentSnapshot) (anchor: SessionAnchor) : StructuralCompletionItem list =
        match DocumentGraph.findNodeAt snapshot anchor.Offset with
        | None -> []
        | Some node ->
            [ { Label = "Insert tab block"
                Description = "Insert tab after current block"
                Edit = InsertBlock(node.Id, "tab", "newTab as \"New\"") } ]
