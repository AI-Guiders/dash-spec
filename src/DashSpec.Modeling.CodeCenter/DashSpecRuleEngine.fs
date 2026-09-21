namespace DashSpec.Modeling.CodeCenter

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Syntax

/// Single-pass rule engine over concept graph (AST walk + graph predicates).
module DashSpecRuleEngine =

    type private BlockFrame =
        { Keyword: DashSpecBlockKeyword
          Identifier: string option
          Span: TextSpan }

    let private moduleFrame (directive: DashSpecModuleDirective) (span: TextSpan) =
        match directive with
        | DashSpecModuleDirective.Dashboard id ->
            { Keyword = DashSpecBlockKeyword.Other "dashboard"
              Identifier = Some id
              Span = span }
        | DashSpecModuleDirective.Tab id ->
            { Keyword = DashSpecBlockKeyword.Tab
              Identifier = Some id
              Span = span }

    let private blockFrame (opener: DashSpecBlockOpener) (span: TextSpan) =
        match opener with
        | DashSpecBlockOpener.Named(keyword, identifier) ->
            { Keyword = keyword
              Identifier = Some identifier
              Span = span }
        | DashSpecBlockOpener.Anonymous keyword ->
            { Keyword = keyword
              Identifier = None
              Span = span }

    /// Tab blocks without a matching `end tab` in the subtree are registry headers, not balance frames.
    let private participatesInBlockBalance (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, id) ->
                DashSpecAst.hasClosingEndBlock node DashSpecBlockKeyword.Tab (Some id)
            | _ -> true
        | _ -> true

    let private parentIndex (edges: DashSpecConceptEdge list) =
        let ancestors = Dictionary<uint32, uint32 list>()

        for edge in edges do
            match ancestors.TryGetValue edge.ChildAstId with
            | true, existing -> ancestors.[edge.ChildAstId] <- edge.ParentAstId :: existing
            | false, _ -> ancestors.[edge.ChildAstId] <- [ edge.ParentAstId ]

        ancestors

    let private hasCardsAncestor (graph: DashSpecConceptGraph) (ancestors: Dictionary<uint32, uint32 list>) (astId: uint32) =
        let rec walk (currentId: uint32) (visited: Set<uint32>) =
            if Set.contains currentId visited then
                false
            else
                match ancestors.TryGetValue currentId with
                | false, _ -> false
                | true, parents ->
                    parents
                    |> List.exists (fun parentId ->
                        match Map.tryFind parentId graph.Nodes with
                        | Some node ->
                            match node.Kind with
                            | DashSpecConceptKind.Block(DashSpecBlockKeyword.Cards, _) -> true
                            | _ -> walk parentId (Set.add currentId visited)
                        | None -> false)

        walk astId Set.empty

    /// Predicate: concept outline span is empty in the built graph.
    let private whenEmptyOutlineSpan (graph: DashSpecConceptGraph) (astId: uint32) =
        match Map.tryFind astId graph.Nodes with
        | Some concept when concept.Span.End <= concept.Span.Start ->
            Some(DashSpecRuleViolation.EmptyOutlineSpan(DashSpecConceptOntology.outlineCaption concept.Kind, concept.Span))
        | _ -> None

    /// Predicate: card reference concept is not nested under a `cards` block.
    let private whenCardOutsideCards
        (graph: DashSpecConceptGraph)
        (ancestors: Dictionary<uint32, uint32 list>)
        (astId: uint32)
        =
        match Map.tryFind astId graph.Nodes with
        | Some concept ->
            match concept.Kind with
            | DashSpecConceptKind.CardReference cardId when not (hasCardsAncestor graph ancestors astId) ->
                Some(DashSpecRuleViolation.CardReferenceOutsideCards(cardId, concept.Span))
            | _ -> None
        | None -> None

    /// One document-order pass: block-balance stack machine + concept graph predicates.
    let evaluateViolations (graph: DashSpecConceptGraph) : DashSpecRuleViolation list =
        let violations = ResizeArray<DashSpecRuleViolation>()
        let blockStack = Stack<BlockFrame>()
        let ancestors = parentIndex graph.Edges

        let signal violation = violations.Add(violation)

        let rec walk (node: DashSpecAstNode) =
            if DashSpecAst.isOutlineNode node then
                let astId = DashSpecAst.id node

                match whenEmptyOutlineSpan graph astId with
                | Some violation -> signal violation
                | None -> ()

                match whenCardOutsideCards graph ancestors astId with
                | Some violation -> signal violation
                | None -> ()

            match node with
            | DashSpecAstNode.ModuleDeclaration n ->
                blockStack.Push(moduleFrame n.Directive n.Span)

                for child in DashSpecAst.members node do
                    walk child

            | DashSpecAstNode.BlockDeclaration n ->
                if participatesInBlockBalance node then
                    blockStack.Push(blockFrame n.Opener n.Span)

                for child in DashSpecAst.members node do
                    walk child

            | DashSpecAstNode.EndBlock n ->
                if blockStack.Count = 0 then
                    signal (DashSpecRuleViolation.ExtraEndBlock n.Span)
                else
                    let frame = blockStack.Pop()

                    if frame.Keyword <> n.EndKeyword then
                        signal (
                            DashSpecRuleViolation.MismatchedEndKeyword(frame.Keyword, n.EndKeyword, n.Span)
                        )

                    match frame.Identifier, n.EndId with
                    | Some openerId, Some endId when not (String.Equals(openerId, endId, StringComparison.Ordinal)) ->
                        signal (DashSpecRuleViolation.MismatchedEndIdentifier(openerId, endId, n.Span))
                    | _ -> ()

            | _ ->
                for child in DashSpecAst.members node do
                    walk child

        walk graph.Tree.Root

        if blockStack.Count > 0 then
            let frame = blockStack.Peek()
            signal (DashSpecRuleViolation.UnclosedBlock(frame.Keyword, frame.Span))

        violations |> Seq.toList

    let evaluate (graph: DashSpecConceptGraph) : ProfileLawDiagnostic list =
        evaluateViolations graph |> List.map DashSpecDiagnosticCatalog.toDiagnostic

    let evaluateBlockBalance (graph: DashSpecConceptGraph) =
        evaluateViolations graph
        |> List.filter DashSpecRuleRegistry.isBlockBalance
        |> List.map DashSpecDiagnosticCatalog.toDiagnostic
