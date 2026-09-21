namespace DashSpec.Modeling.CodeCenter

open AIGuiders.Platform.Modeling.Core.Identity
open DashSpec.Modeling.Parse.Syntax

/// Planet tiers over the shared AST (GUIDERS-ADR-0067 §2).
[<RequireQualifiedAccess>]
type DashSpecProjectionRole =
    | Outline
    | Diagram
    | FormField

[<RequireQualifiedAccess>]
type DashSpecConceptKind =
    | CompilationUnit
    | DashboardModule of identifier: string
    | TabModule of identifier: string
    | Block of keyword: DashSpecBlockKeyword * identifier: string option
    | CardReference of cardId: string

[<RequireQualifiedAccess>]
type DashSpecConceptEdgeKind =
    | Contains

/// Semantic tier attached to an AST outline node.
type DashSpecConceptTier =
    { Id: NodeId
      Kind: DashSpecConceptKind
      Span: TextSpan
      Title: string option
      ProjectionRole: DashSpecProjectionRole }

type DashSpecConceptEdge =
    { ParentId: NodeId
      ChildId: NodeId
      Kind: DashSpecConceptEdgeKind }

/// AST + planet tiers (same node ids end-to-end).
type DashSpecConceptGraph =
    { Tree: ParseTree
      RootId: NodeId
      Tiers: Map<NodeId, DashSpecConceptTier>
      Edges: DashSpecConceptEdge list }

type ProfileLawDiagnostic =
    { Code: string
      Message: string
      Start: int
      Length: int
      Severity: string }

module DashSpecConceptOntology =

    let projectionRole (kind: DashSpecConceptKind) =
        match kind with
        | DashSpecConceptKind.CompilationUnit -> DashSpecProjectionRole.Outline
        | DashSpecConceptKind.DashboardModule _
        | DashSpecConceptKind.TabModule _
        | DashSpecConceptKind.CardReference _ -> DashSpecProjectionRole.Diagram
        | DashSpecConceptKind.Block(keyword, _) ->
            if DashSpecBlockKeyword.isDiagramContainer keyword then
                DashSpecProjectionRole.Diagram
            elif DashSpecBlockKeyword.isFormField keyword then
                DashSpecProjectionRole.FormField
            else
                DashSpecProjectionRole.Outline

    let kindFromAst (node: DashSpecAstNode) =
        match node with
        | DashSpecAstNode.CompilationUnit _ -> DashSpecConceptKind.CompilationUnit
        | DashSpecAstNode.ModuleDeclaration n ->
            match n.Directive with
            | DashSpecModuleDirective.Dashboard id -> DashSpecConceptKind.DashboardModule id
            | DashSpecModuleDirective.Tab id -> DashSpecConceptKind.TabModule id
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(keyword, identifier) -> DashSpecConceptKind.Block(keyword, Some identifier)
            | DashSpecBlockOpener.Anonymous keyword -> DashSpecConceptKind.Block(keyword, None)
        | DashSpecAstNode.CardReference n -> DashSpecConceptKind.CardReference n.CardId
        | _ -> failwith "not a concept-bearing AST node"

    let outlineCaption (kind: DashSpecConceptKind) =
        match kind with
        | DashSpecConceptKind.CompilationUnit -> "unit"
        | DashSpecConceptKind.DashboardModule id -> $"@dashboard {id}"
        | DashSpecConceptKind.TabModule id -> $"@tab {id}"
        | DashSpecConceptKind.Block(keyword, Some identifier) ->
            $"{DashSpecBlockKeyword.toEndName keyword} {identifier}"
        | DashSpecConceptKind.Block(keyword, None) -> DashSpecBlockKeyword.toEndName keyword
        | DashSpecConceptKind.CardReference cardId -> $"card {cardId}"

    let treeCaption (tier: DashSpecConceptTier) =
        match tier.Title with
        | Some title -> title
        | None -> outlineCaption tier.Kind
