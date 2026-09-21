namespace DashSpec.Modeling.CodeCenter

open System
open AIGuiders.Platform.Modeling.CodeCenter
open AIGuiders.Platform.Modeling.Core.Identity
open DashSpec.Modeling.Parse.Syntax

/// AST-first structural edits: mutate text at tier-aware offsets, then rebuild AST + tiers.
module DashSpecStructuralPlanner =

    let private lineIndent (text: string) (offset: int) =
        let lineStart =
            if offset <= 0 then
                0
            else
                text.LastIndexOf('\n', offset - 1) + 1

        let mutable column = lineStart

        while column < text.Length && text.[column] = ' ' do
            column <- column + 1

        text.Substring(lineStart, column - lineStart)

    let private findAstNode (root: DashSpecAstNode) (nodeId: NodeId) =
        let rec walk (node: DashSpecAstNode) =
            if DashSpecAst.id node = nodeId then
                Some node
            else
                DashSpecAst.members node |> Array.tryPick walk

        walk root

    let private isContainer (tier: DashSpecConceptTier) =
        match tier.Kind with
        | DashSpecConceptKind.DashboardModule _
        | DashSpecConceptKind.TabModule _
        | DashSpecConceptKind.Block _ -> true
        | _ -> false

    let private expectedEndKeyword (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.ModuleDeclaration n ->
            match n.Directive with
            | DashSpecModuleDirective.Dashboard _ -> Some(DashSpecBlockKeyword.Other "dashboard")
            | DashSpecModuleDirective.Tab _ -> Some DashSpecBlockKeyword.Tab
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(keyword, _) -> Some keyword
            | DashSpecBlockOpener.Anonymous keyword -> Some keyword
        | _ -> None

    let private closingEndOffset (node: DashSpecAstNode) =
        match expectedEndKeyword node with
        | None -> None
        | Some keyword ->
            DashSpecAst.descendants node
            |> Seq.tryFind (function
                | DashSpecAstNode.EndBlock n when n.EndKeyword = keyword -> true
                | _ -> false)
            |> Option.map (fun endNode -> (DashSpecAst.span endNode).Start)

    let private memberLineIndent (text: string) (ast: DashSpecAstNode) =
        let members =
            DashSpecAst.members ast
            |> Array.filter (fun child -> not (DashSpecAst.isEndBlock child))

        match members |> Array.tryLast with
        | Some lastChild -> lineIndent text (DashSpecAst.span lastChild).Start
        | None ->
            let headerIndent = lineIndent text (DashSpecAst.span ast).Start
            headerIndent + "    "

    let private insertionOffset (graph: DashSpecConceptGraph) (anchorId: NodeId) =
        match Map.tryFind anchorId graph.Tiers, findAstNode graph.Tree.Root anchorId with
        | None, _ -> Error $"anchor {anchorId} not found in concept tiers"
        | Some tier, None -> Error $"anchor {anchorId} not found in AST"
        | Some tier, Some ast ->
            let text = graph.Tree.Text

            let offset, indent =
                if isContainer tier then
                    let offset = closingEndOffset ast |> Option.defaultValue tier.Span.End
                    offset, memberLineIndent text ast
                else
                    tier.Span.End, lineIndent text tier.Span.Start

            Ok(offset, indent)

    let private insertSourceLine (text: string) (offset: int) (indent: string) (sourceLine: string) =
        let trimmed = sourceLine.Trim()
        let block = Environment.NewLine + indent + trimmed + Environment.NewLine
        text.Insert(offset, block)

    let private rebuildSnapshot (text: string) =
        let snapshot, _, _ = DashSpecProfileRebuild.rebuild text
        snapshot

    let private planInsertBlock (before: DocumentSnapshot) (anchorId: NodeId) (sourceLine: string) =
        let graph, _ = DashSpecSerializeRules.parseAndBuild before.Text

        match insertionOffset graph anchorId with
        | Error message -> Error message
        | Ok(offset, indent) ->
            let newText = insertSourceLine before.Text offset indent sourceLine

            Ok
                { Snapshot = rebuildSnapshot newText
                  Inverse = None
                  InverseQuality = InverseQuality.Unspecified }

    let private planRenameMember (before: DocumentSnapshot) (nodeId: NodeId) (newName: string) =
        match DocumentGraph.renameNode before nodeId newName with
        | Error message -> Error message
        | Ok renamed ->
            let snapshot = rebuildSnapshot renamed.Text
            let inverse =
                Map.tryFind nodeId before.Nodes
                |> Option.map (fun node -> RenameMember(nodeId, node.Name))

            Ok
                { Snapshot = snapshot
                  Inverse = inverse
                  InverseQuality = InverseQuality.Exact }

    let plan (before: DocumentSnapshot) (edit: StructuralEdit) : Result<StructuralPlanOutcome, string> =
        match edit with
        | InsertBlock(anchorId, sourceLine) -> planInsertBlock before anchorId sourceLine
        | RenameMember(nodeId, newName) -> planRenameMember before nodeId newName
        | other ->
            match StructuralPlanGraph.plan before other with
            | Error message -> Error message
            | Ok outcome -> Ok { outcome with Snapshot = rebuildSnapshot outcome.Snapshot.Text }
