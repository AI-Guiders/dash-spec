namespace DashSpec.Modeling.Parse.Syntax

open System
open AIGuiders.Platform.Modeling.Core.Identity
open DashSpec.Modeling.Core

[<RequireQualifiedAccess>]
type SourceLeafKindSyntax =
    | View
    | SqlQuery
    | SqlFile
    | Xlsx

[<RequireQualifiedAccess>]
type CompositeOriginKindSyntax =
    | InnerJoin
    | LeftJoin
    | RightJoin
    | FullJoin
    | UnionAll

/// <summary>Typed source origin (ADR-0126 / ADR-0130): shared between header `= Origin(…)`
/// and body `from view|sql|xlsx …` so both forms project the same structured AST.</summary>
[<RequireQualifiedAccess>]
type SourceOriginSyntax =
    | Leaf of kind: SourceLeafKindSyntax * target: string * sheet: string option
    | Composite of kind: CompositeOriginKindSyntax * left: string * right: string

[<RequireQualifiedAccess>]
type PortShapeSyntax =
    | Stream
    | Scalar

[<RequireQualifiedAccess>]
type PortDirection =
    | Input
    | Output

[<RequireQualifiedAccess>]
type LayoutScopeSyntax =
    | Toolbar
    | Section
    | Page
    | Card
    | Host

[<RequireQualifiedAccess>]
type ProviderBindingSyntax =
    | Infer
    | Named of id: string

/// <summary>Positional clause family (ADR-0130): each directive line carries its exact
/// parsed positional structure rather than an untyped string bag.</summary>
[<RequireQualifiedAccess>]
type ClauseKind =
    | NamespaceClause of name: string
    | ScopeClause of scope: LayoutScopeSyntax
    | UseProviderClause of provider: ProviderBindingSyntax
    | UseClause of kind: string * target: string
    | FromClause of origin: SourceOriginSyntax
    | DefaultPortClause of direction: PortDirection * portName: string
    | DirectiveClause of keyword: string * args: string[]

/// <summary>First-class compilation-unit kinds (ADR-0101 / ADR-0130): all 12 module
/// kinds of the language are explicit DU cases.</summary>
[<RequireQualifiedAccess>]
type DashSpecModuleKind =
    | Dashboard
    | Section
    | Diagram
    | Layout
    | Types
    | Flow of emitViews: bool
    | Presentation
    | Tooltip
    | Palette
    | Catalog
    | Host
    | Transform
    | Custom of keyword: string

module DashSpecModuleKind =

    let ofKeyword (keyword: string) (emitViews: bool) =
        match keyword.Trim().ToLowerInvariant() with
        | "dashboard" -> DashSpecModuleKind.Dashboard
        | "section" -> DashSpecModuleKind.Section
        | "diagram" -> DashSpecModuleKind.Diagram
        | "layout" -> DashSpecModuleKind.Layout
        | "types" -> DashSpecModuleKind.Types
        | "flow" -> DashSpecModuleKind.Flow emitViews
        | "presentation" -> DashSpecModuleKind.Presentation
        | "tooltip" -> DashSpecModuleKind.Tooltip
        | "palette" -> DashSpecModuleKind.Palette
        | "catalog" -> DashSpecModuleKind.Catalog
        | "host" -> DashSpecModuleKind.Host
        | "transform" -> DashSpecModuleKind.Transform
        | other -> DashSpecModuleKind.Custom other

    let toKeyword (kind: DashSpecModuleKind) =
        match kind with
        | DashSpecModuleKind.Dashboard -> "dashboard"
        | DashSpecModuleKind.Section -> "section"
        | DashSpecModuleKind.Diagram -> "diagram"
        | DashSpecModuleKind.Layout -> "layout"
        | DashSpecModuleKind.Types -> "types"
        | DashSpecModuleKind.Flow _ -> "flow"
        | DashSpecModuleKind.Presentation -> "presentation"
        | DashSpecModuleKind.Tooltip -> "tooltip"
        | DashSpecModuleKind.Palette -> "palette"
        | DashSpecModuleKind.Catalog -> "catalog"
        | DashSpecModuleKind.Host -> "host"
        | DashSpecModuleKind.Transform -> "transform"
        | DashSpecModuleKind.Custom kw -> kw

[<RequireQualifiedAccess>]
type DashSpecModuleDirective =
    | Dashboard of identifier: string
    | Tab of identifier: string
    | Module of kind: DashSpecModuleKind * identifier: string

type DashSpecBlockOpener =
    | Named of keyword: DashSpecBlockKeyword * identifier: string
    | Anonymous of keyword: DashSpecBlockKeyword

/// Roslyn-style typed syntax tree for DashSpec block surface (planet SSOT).
[<RequireQualifiedAccess>]
type DashSpecAstNode =
    | CompilationUnit of CompilationUnitSyntax
    | ModuleDeclaration of ModuleDeclarationSyntax
    | BlockDeclaration of BlockDeclarationSyntax
    | CardReference of CardReferenceSyntax
    | EndBlock of EndBlockSyntax
    | Line of LineSyntax
    | BlankLine of BlankLineSyntax
    | ImportDeclaration of ImportDeclarationSyntax
    | TypeFieldDeclaration of TypeFieldSyntax
    | PropertyAssignment of PropertySyntax
    | PortDeclaration of PortDeclarationSyntax
    | FlowLinkDeclaration of FlowLinkSyntax
    | ClauseDeclaration of ClauseSyntax
    | GridRowDeclaration of GridRowSyntax
    | TableRowDeclaration of TableRowSyntax

and CompilationUnitSyntax =
    { Id: NodeId
      Span: TextSpan
      Members: DashSpecAstNode[] }

and ModuleDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      Directive: DashSpecModuleDirective
      HeaderSpan: TextSpan
      Tokens: SyntaxToken[]
      Members: DashSpecAstNode[] }

and BlockDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      Opener: DashSpecBlockOpener
      TypeAnnotation: string option
      Origin: SourceOriginSyntax option
      HeaderSpan: TextSpan
      Tokens: SyntaxToken[]
      Members: DashSpecAstNode[] }

and EndBlockSyntax =
    { Id: NodeId
      Span: TextSpan
      EndKeyword: DashSpecBlockKeyword
      EndId: string option
      Tokens: SyntaxToken[] }

and CardReferenceSyntax =
    { Id: NodeId
      Span: TextSpan
      CardId: string
      Tokens: SyntaxToken[] }

and LineSyntax =
    { Id: NodeId
      Span: TextSpan
      Tokens: SyntaxToken[] }

and BlankLineSyntax = { Id: NodeId; Span: TextSpan }

/// <summary>Module-header import edge: `import &lt;kind&gt; from &lt;namespace&gt; [as &lt;alias&gt;]` (ADR-0098).</summary>
and ImportDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      /// Surface keyword (`import`).
      Keyword: string
      /// Unit kind keyword (`types`/`flows`/`layouts`/…).
      Kind: string option
      /// Resolved typed ImportKind when recognized by ImportKindRegistry.
      ImportKind: ImportKind option
      /// Imported namespace.
      Target: string
      Alias: string option
      Tokens: SyntaxToken[] }

/// <summary>Typed field declaration inside a `type &lt;Name&gt;` block: `[optional] &lt;type&gt; &lt;name&gt;` (ADR-0130).</summary>
and TypeFieldSyntax =
    { Id: NodeId
      Span: TextSpan
      Optional: bool
      FieldType: DashType
      FieldName: string
      Tokens: SyntaxToken[] }

/// <summary>Typed property assignment inside a block: `&lt;key&gt; = &lt;value&gt;` (ADR-0130).</summary>
and PropertySyntax =
    { Id: NodeId
      Span: TextSpan
      Key: string
      ValueText: string
      KeyTokens: SyntaxToken[]
      EqualsToken: SyntaxToken
      ValueTokens: SyntaxToken[]
      Tokens: SyntaxToken[] }

/// <summary>Port-role of a ports-block member (ADR-0130).</summary>
and PortDeclRole =
    | PortInput
    | PortOutput
    | PortParam

/// <summary>Typed member of a `ports … end ports` block (ADR-0130):
/// `input stream|scalar Name: Type`, `output stream Name: Type`,
/// `param name: type [= default] [bind column]`.</summary>
and PortDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      Role: PortDeclRole
      PortShape: PortShapeSyntax option
      PortName: string
      ValueType: string option
      DefaultValue: string option
      ColumnBinding: string option
      Tokens: SyntaxToken[] }

/// <summary>Typed flow edge inside a flow module (ADR-0130):
/// `producer [outPort] -&gt; [inPort] consumer`.</summary>
and FlowLinkSyntax =
    { Id: NodeId
      Span: TextSpan
      FromNode: string
      FromPort: string option
      ToNode: string
      ToPort: string option
      Tokens: SyntaxToken[] }

/// <summary>Positional directive line without an assignment (ADR-0130):
/// `namespace`, `scope`, `use`, `from`, `default input|output`, or positional directives.</summary>
and ClauseSyntax =
    { Id: NodeId
      Span: TextSpan
      Kind: ClauseKind
      Tokens: SyntaxToken[] }

/// <summary>Typed cell inside a grid drawing row `[ card:weight ] [ ]` (ADR-0111 §2 / ADR-0130).</summary>
and GridCellSyntax =
    { Target: string
      Weight: int option }

    member this.RawText =
        match this.Weight with
        | Some w -> $"{this.Target}:{w}"
        | None -> this.Target

/// <summary>Grid drawing row `[ card:weight ] [ ]` (ADR-0111 §2) — empty cell is
/// the whole `[]` token; cells carry both structured `TypedCells` and `Cells` text.</summary>
and GridRowSyntax =
    { Id: NodeId
      Span: TextSpan
      TypedCells: GridCellSyntax[]
      Cells: string[]
      Tokens: SyntaxToken[] }

/// <summary>Markdown-style table row `| a | b |` — one TableRow token per line.</summary>
and TableRowSyntax =
    { Id: NodeId
      Span: TextSpan
      Cells: string[]
      Tokens: SyntaxToken[] }

[<CLIMutable>]
type ParseTree =
    { Text: string
      Root: DashSpecAstNode
      Tokens: SyntaxToken[] }

/// DU discriminant for DashSpecAstNode (interop + diagnostics).
type DashSpecAstNodeKind =
    | CompilationUnit = 0
    | ModuleDeclaration = 1
    | BlockDeclaration = 2
    | CardReference = 3
    | EndBlock = 4
    | Line = 5
    | BlankLine = 6
    | ImportDeclaration = 7
    | TypeFieldDeclaration = 8
    | PropertyAssignment = 9
    | PortDeclaration = 10
    | FlowLinkDeclaration = 11
    | ClauseDeclaration = 12
    | GridRowDeclaration = 13
    | TableRowDeclaration = 14
