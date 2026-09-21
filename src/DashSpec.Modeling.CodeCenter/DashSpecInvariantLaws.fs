namespace DashSpec.Modeling.CodeCenter

open System
open System.Collections.Generic
open DashSpec.Modeling.Parse.Syntax

/// Pure invariant laws over DashSpec concept graph + AST (ADR-0067 §2, §9).
module DashSpecInvariantLaws =

    type private Frame =
        { Keyword: DashSpecBlockKeyword
          Identifier: string option
          Span: TextSpan }

    let private diagnostic code message start length severity =
        { Code = code
          Message = message
          Start = start
          Length = max 1 length
          Severity = severity }

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

    /// Tab lines with `as "title"` are header-only and do not take a matching `end tab`.
    let private isHeaderOnlyTab (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, _) ->
                n.Tokens |> Array.exists (fun token -> token.Text = "as")
            | _ -> false
        | _ -> false

    let balancedBlockStructure (graph: DashSpecConceptGraph) =
        let diagnostics = ResizeArray<ProfileLawDiagnostic>()
        let stack = Stack<Frame>()

        let rec walk (node: DashSpecAstNode) =
            match node with
            | DashSpecAstNode.ModuleDeclaration n ->
                stack.Push(moduleFrame n.Directive n.Span)

                for child in DashSpecAst.members node do
                    walk child

            | DashSpecAstNode.BlockDeclaration n ->
                if not (isHeaderOnlyTab node) then
                    stack.Push(blockFrame n.Opener n.Span)

                for child in DashSpecAst.members node do
                    walk child

            | DashSpecAstNode.EndBlock n ->
                if stack.Count = 0 then
                    diagnostics.Add(
                        diagnostic
                            "DS002"
                            "unexpected end block"
                            n.Span.Start
                            n.Span.Length
                            "error")
                else
                    let frame = stack.Pop()

                    if frame.Keyword <> n.EndKeyword then
                        diagnostics.Add(
                            diagnostic
                                "DS003"
                                $"end {DashSpecBlockKeyword.toEndName n.EndKeyword} does not match opener {DashSpecBlockKeyword.toEndName frame.Keyword}"
                                n.Span.Start
                                n.Span.Length
                                "error")

                    match frame.Identifier, n.EndId with
                    | Some openerId, Some endId when not (String.Equals(openerId, endId, StringComparison.Ordinal)) ->
                        diagnostics.Add(
                            diagnostic
                                "DS004"
                                $"end block identifier '{endId}' does not match opener '{openerId}'"
                                n.Span.Start
                                n.Span.Length
                                "error")
                    | _ -> ()

            | _ ->
                for child in DashSpecAst.members node do
                    walk child

        walk graph.Tree.Root

        if stack.Count > 0 then
            let frame = stack.Peek()

            diagnostics.Add(
                diagnostic
                    "DS005"
                    $"unclosed block '{DashSpecBlockKeyword.toEndName frame.Keyword}'"
                    frame.Span.Start
                    frame.Span.Length
                    "error")

        diagnostics |> Seq.toList

    let outlineSpansAreValid (graph: DashSpecConceptGraph) =
        graph.Nodes
        |> Map.toList
        |> List.choose (fun (_, node) ->
            if node.Span.End > node.Span.Start then
                None
            else
                Some(
                    diagnostic
                        "DS006"
                        $"outline node '{node.Label}' has empty span"
                        node.Span.Start
                        1
                        "error"))

    let cardReferencesUnderCards (graph: DashSpecConceptGraph) =
        let ancestors = System.Collections.Generic.Dictionary<uint32, uint32 list>()

        for edge in graph.Edges do
            match ancestors.TryGetValue edge.ChildAstId with
            | true, existing -> ancestors.[edge.ChildAstId] <- edge.ParentAstId :: existing
            | false, _ -> ancestors.[edge.ChildAstId] <- [ edge.ParentAstId ]

        let rec hasCardsAncestor (astId: uint32) (visited: Set<uint32>) =
            if Set.contains astId visited then
                false
            else
                match ancestors.TryGetValue astId with
                | false, _ -> false
                | true, parents ->
                    parents
                    |> List.exists (fun parentId ->
                        match Map.tryFind parentId graph.Nodes with
                        | Some node ->
                            match node.Kind with
                            | DashSpecConceptKind.Block(DashSpecBlockKeyword.Cards, _) -> true
                            | _ -> hasCardsAncestor parentId (Set.add astId visited)
                        | None -> false)

        graph.Nodes
        |> Map.toList
        |> List.choose (fun (_, node) ->
            match node.Kind with
            | DashSpecConceptKind.CardReference cardId when not (hasCardsAncestor node.AstId Set.empty) ->
                Some(
                    diagnostic
                        "DS007"
                        $"card reference '{cardId}' is not nested under a cards block"
                        node.Span.Start
                        node.Span.Length
                        "warning")
            | _ -> None)

    let all (graph: DashSpecConceptGraph) =
        [ balancedBlockStructure graph
          outlineSpansAreValid graph
          cardReferencesUnderCards graph ]
        |> List.concat
