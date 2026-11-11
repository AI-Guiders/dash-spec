namespace DashSpec.CodeCenter

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
    | UnitModule of kind: DashSpecModuleKind * identifier: string
    | ImportDeclaration of keyword: string * target: string
    | Block of keyword: DashSpecBlockKeyword * identifier: string option
    | CardReference of cardId: string
    | TypeField of fieldName: string
    | Property of key: string
    | Port of portName: string
    | FlowLink of fromNode: string * toNode: string
    | Clause of kind: ClauseKind
    | GridRow
    | TableRow

[<RequireQualifiedAccess>]
type DashSpecConceptEdgeKind =
    | Contains

/// Semantic tier attached to an AST concept node.
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
        | DashSpecConceptKind.UnitModule _
        | DashSpecConceptKind.ImportDeclaration _ -> DashSpecProjectionRole.Outline
        | DashSpecConceptKind.DashboardModule _
        | DashSpecConceptKind.TabModule _
        | DashSpecConceptKind.CardReference _ -> DashSpecProjectionRole.Diagram
        | DashSpecConceptKind.TypeField _
        | DashSpecConceptKind.Property _ -> DashSpecProjectionRole.FormField
        | DashSpecConceptKind.FlowLink _ -> DashSpecProjectionRole.Diagram
        | DashSpecConceptKind.Port _
        | DashSpecConceptKind.Clause _
        | DashSpecConceptKind.TableRow -> DashSpecProjectionRole.FormField
        | DashSpecConceptKind.GridRow -> DashSpecProjectionRole.Diagram
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
            | DashSpecModuleDirective.Module(kind, id) -> DashSpecConceptKind.UnitModule(kind, id)
        | DashSpecAstNode.ImportDeclaration n ->
            DashSpecConceptKind.ImportDeclaration(n.Keyword, n.Target)
        | DashSpecAstNode.BlockDeclaration n ->
            match n.Opener with
            | DashSpecBlockOpener.Named(keyword, identifier) -> DashSpecConceptKind.Block(keyword, Some identifier)
            | DashSpecBlockOpener.Anonymous keyword -> DashSpecConceptKind.Block(keyword, None)
        | DashSpecAstNode.CardReference n -> DashSpecConceptKind.CardReference n.CardId
        | DashSpecAstNode.TypeFieldDeclaration n -> DashSpecConceptKind.TypeField n.FieldName
        | DashSpecAstNode.PropertyAssignment n -> DashSpecConceptKind.Property n.Key
        | DashSpecAstNode.PortDeclaration n -> DashSpecConceptKind.Port n.PortName
        | DashSpecAstNode.FlowLinkDeclaration n -> DashSpecConceptKind.FlowLink(n.FromNode, n.ToNode)
        | DashSpecAstNode.ClauseDeclaration n -> DashSpecConceptKind.Clause n.Kind
        | DashSpecAstNode.GridRowDeclaration _ -> DashSpecConceptKind.GridRow
        | DashSpecAstNode.TableRowDeclaration _ -> DashSpecConceptKind.TableRow
        | _ -> failwith "not a concept-bearing AST node"

    let clauseCaption (kind: ClauseKind) =
        match kind with
        | ClauseKind.NamespaceClause ns -> $"namespace {ns}"
        | ClauseKind.ScopeClause s -> $"scope {s}"
        | ClauseKind.UseProviderClause p -> $"use provider {p}"
        | ClauseKind.UseClause(k, t) -> $"use {k} {t}"
        | ClauseKind.FromClause _ -> "from"
        | ClauseKind.DefaultPortClause(dir, port) -> $"default {dir} {port}"
        | ClauseKind.DirectiveClause(kw, _) -> kw

    let outlineCaption (kind: DashSpecConceptKind) =
        match kind with
        | DashSpecConceptKind.CompilationUnit -> "unit"
        | DashSpecConceptKind.DashboardModule id -> $"module dashboard {id}"
        | DashSpecConceptKind.TabModule id -> $"module tab {id}"
        | DashSpecConceptKind.UnitModule(kind, id) -> $"module {DashSpecModuleKind.toKeyword kind} {id}"
        | DashSpecConceptKind.ImportDeclaration(keyword, target) -> $"{keyword} {target}"
        | DashSpecConceptKind.Block(keyword, Some identifier) ->
            $"{DashSpecBlockKeyword.toEndName keyword} {identifier}"
        | DashSpecConceptKind.Block(keyword, None) -> DashSpecBlockKeyword.toEndName keyword
        | DashSpecConceptKind.CardReference cardId -> $"card {cardId}"
        | DashSpecConceptKind.TypeField fieldName -> $"field {fieldName}"
        | DashSpecConceptKind.Property key -> $"property {key}"
        | DashSpecConceptKind.Port portName -> $"port {portName}"
        | DashSpecConceptKind.FlowLink(fromNode, toNode) -> $"{fromNode} -> {toNode}"
        | DashSpecConceptKind.Clause clauseKind -> clauseCaption clauseKind
        | DashSpecConceptKind.GridRow -> "grid row"
        | DashSpecConceptKind.TableRow -> "table row"

    let treeCaption (tier: DashSpecConceptTier) =
        match tier.Title with
        | Some title -> title
        | None -> outlineCaption tier.Kind
