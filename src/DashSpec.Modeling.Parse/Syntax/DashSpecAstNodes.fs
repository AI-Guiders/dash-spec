namespace DashSpec.Modeling.Parse.Syntax

open System
open AIGuiders.Platform.Modeling.Core.Identity
open DashSpec.Modeling.Core

/// <summary>First-class member-access / qualified identifier tree (`Ident` or `target . member`) in DashSpec surface syntax.</summary>
[<RequireQualifiedAccess>]
type DotSyntax =
    | Ident of name: string
    | Member of target: DotSyntax * memberName: string

    member this.Segments : string[] =
        let rec loop acc node =
            match node with
            | DotSyntax.Ident name -> (name :: acc) |> List.toArray
            | DotSyntax.Member(target, memberName) -> loop (memberName :: acc) target
        loop [] this

    member this.Text = String.Join(".", this.Segments)
    member this.IsQualified =
        match this with
        | DotSyntax.Member _ -> true
        | DotSyntax.Ident _ -> false
    member this.Root =
        match this with
        | DotSyntax.Ident name -> name
        | DotSyntax.Member(target, _) -> target.Root
    member this.MemberName =
        match this with
        | DotSyntax.Ident name -> name
        | DotSyntax.Member(_, memberName) -> memberName
    override this.ToString() = this.Text

module DotSyntax =

    let ofSegments (segments: string seq) : DotSyntax =
        let clean =
            segments
            |> Seq.map (fun s -> s.Trim())
            |> Seq.filter (fun s -> s.Length > 0)
            |> Seq.toList

        match clean with
        | [] -> DotSyntax.Ident ""
        | head :: tail ->
            tail |> List.fold (fun acc seg -> DotSyntax.Member(acc, seg)) (DotSyntax.Ident head)

    let parse (raw: string) : DotSyntax =
        if String.IsNullOrWhiteSpace raw then
            DotSyntax.Ident ""
        else
            ofSegments (raw.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries))

/// <summary>Port selector inside brackets `[port]` or `[innerNode.port]` on a flow arrow endpoint.</summary>
[<RequireQualifiedAccess>]
type PortSelectorSyntax =
    | Port of name: string
    | QualifiedPort of innerNode: string * portName: string

    member this.RawText =
        match this with
        | PortSelectorSyntax.Port name -> name
        | PortSelectorSyntax.QualifiedPort(n, p) -> $"{n}.{p}"

module PortSelectorSyntax =

    let parse (raw: string) : PortSelectorSyntax =
        match DotSyntax.parse raw with
        | DotSyntax.Member(DotSyntax.Ident node, port) -> PortSelectorSyntax.QualifiedPort(node, port)
        | d -> PortSelectorSyntax.Port d.Text

/// <summary>First-class semantic target of a flow arrow endpoint (`producer -> consumer`),
/// parsed directly from its <see cref="DotSyntax"/> structure.</summary>
[<RequireQualifiedAccess>]
type FlowEndpointTarget =
    | ImplicitDiagram
    | ReportAnchor
    | PageAnchor of pageId: string
    | CardAnchor of cardId: string
    | CardSlot of cardId: string * slot: string
    | DashboardChrome
    | SectionChrome of sectionId: string
    | CardChrome of cardId: string
    | HostTarget of path: DotSyntax
    | PhaseTarget of phaseId: string
    | FilterNode of filterId: string
    | Node of path: DotSyntax

module FlowEndpointTarget =

    let ofDotSyntax (path: DotSyntax) : FlowEndpointTarget =
        match path.Segments |> Array.map (fun s -> s.Trim()) with
        | [| "__diagram__" |] -> FlowEndpointTarget.ImplicitDiagram
        | [| s |] when String.Equals(s, "report", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.ReportAnchor
        | [| prefix; pageId |] when String.Equals(prefix, "page", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.PageAnchor pageId
        | [| prefix; cardId |] when String.Equals(prefix, "card", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.CardAnchor cardId
        | [| prefix; cardId; slot |] when String.Equals(prefix, "card", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.CardSlot(cardId, slot)
        | [| prefix; target |]
            when String.Equals(prefix, "chrome", StringComparison.OrdinalIgnoreCase)
                 && String.Equals(target, "dashboard", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.DashboardChrome
        | [| prefix; kind; secId |]
            when String.Equals(prefix, "chrome", StringComparison.OrdinalIgnoreCase)
                 && (String.Equals(kind, "section", StringComparison.OrdinalIgnoreCase)
                     || String.Equals(kind, "tab", StringComparison.OrdinalIgnoreCase)) ->
            FlowEndpointTarget.SectionChrome secId
        | [| prefix; kind; cardId |]
            when String.Equals(prefix, "chrome", StringComparison.OrdinalIgnoreCase)
                 && String.Equals(kind, "card", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.CardChrome cardId
        | segs when segs.Length >= 2 && String.Equals(segs.[0], "host", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.HostTarget(DotSyntax.ofSegments segs.[1..])
        | [| prefix; phaseId |] when String.Equals(prefix, "phase", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.PhaseTarget phaseId
        | [| prefix; filterId |] when String.Equals(prefix, "filter", StringComparison.OrdinalIgnoreCase) ->
            FlowEndpointTarget.FilterNode filterId
        | _ ->
            FlowEndpointTarget.Node path

/// <summary>Typed endpoint of a flow arrow (`Source -> Target`), combining <see cref="FlowEndpointTarget"/>,
/// <see cref="DotSyntax"/> path, and optional <see cref="PortSelectorSyntax"/>.</summary>
type FlowEndpointSyntax =
    { Target: FlowEndpointTarget
      Path: DotSyntax
      Port: PortSelectorSyntax option }

module FlowEndpointSyntax =

    let create (rawNode: string) (rawPort: string option) : FlowEndpointSyntax =
        let path = DotSyntax.parse rawNode
        { Target = FlowEndpointTarget.ofDotSyntax path
          Path = path
          Port = rawPort |> Option.map PortSelectorSyntax.parse }

/// <summary>First-class typed right-hand-side expression of a `key = value` property assignment (ADR-0130),
/// backed by validated domain value types (<see cref="HexColorValue"/>, <see cref="TimeShiftValue"/>,
/// <see cref="IanaZoneValue"/>, <see cref="RelativeDayValue"/>, <see cref="DateBoundValue"/>, <see cref="DateRangeValue"/>).</summary>
[<RequireQualifiedAccess>]
type PropertyValueSyntax =
    | Member of path: DotSyntax
    | StringLiteral of value: string
    | Boolean of value: bool
    | Integer of value: int64
    | Decimal of value: decimal
    | HexColor of color: HexColorValue
    | TimeShift of shift: TimeShiftValue
    | IanaZone of zone: IanaZoneValue
    | RelativeDay of day: RelativeDayValue
    | DateBound of bound: DateBoundValue
    | DateRange of dateRange: DateRangeValue
    | Range of fromBound: PropertyValueSyntax * toBound: PropertyValueSyntax
    | Call of target: DotSyntax * args: PropertyValueSyntax[]
    | BracketList of items: PropertyValueSyntax[]
    | CommaList of items: PropertyValueSyntax[]
    | Aliased of value: PropertyValueSyntax * title: string
    | Raw of text: string

    member this.Text =
        match this with
        | PropertyValueSyntax.Member d -> d.Text
        | PropertyValueSyntax.StringLiteral s -> s
        | PropertyValueSyntax.Boolean b -> if b then "true" else "false"
        | PropertyValueSyntax.Integer i -> string i
        | PropertyValueSyntax.Decimal m -> string m
        | PropertyValueSyntax.HexColor c -> c.Normalized
        | PropertyValueSyntax.TimeShift s -> s.ToString()
        | PropertyValueSyntax.IanaZone z -> z.Id
        | PropertyValueSyntax.RelativeDay d -> d.ToString()
        | PropertyValueSyntax.DateBound b ->
            match b with
            | DateBoundValue.Today -> "today"
            | DateBoundValue.Relative r -> r.ToString()
            | DateBoundValue.Calendar dt -> dt.ToString("yyyy-MM-dd")
        | PropertyValueSyntax.DateRange r -> $"{r.From}..{r.To}"
        | PropertyValueSyntax.Range(a, b) -> $"{a.Text}..{b.Text}"
        | PropertyValueSyntax.Call(t, args) ->
            let argStr = args |> Array.map (fun x -> x.Text) |> String.concat ", "
            $"{t.Text}({argStr})"
        | PropertyValueSyntax.BracketList items ->
            let inner = items |> Array.map (fun x -> x.Text) |> String.concat ", "
            $"[{inner}]"
        | PropertyValueSyntax.CommaList items ->
            items |> Array.map (fun x -> x.Text) |> String.concat ", "
        | PropertyValueSyntax.Aliased(v, title) -> $"{v.Text} as \"{title}\""
        | PropertyValueSyntax.Raw t -> t

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
    | Registry
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
        | "registry" -> DashSpecModuleKind.Registry
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
        | DashSpecModuleKind.Registry -> "registry"
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
      /// Imported namespace as structured DotSyntax.
      TargetPath: DotSyntax
      /// Imported namespace text.
      Target: string
      Alias: string option
      Tokens: SyntaxToken[] }

/// <summary>Typed field declaration inside a `type &lt;Name&gt;` block: `[optional] &lt;type&gt; &lt;name&gt;` (ADR-0130).</summary>
and TypeFieldSyntax =
    { Id: NodeId
      Span: TextSpan
      Optional: bool
      FieldType: DashType
      FieldPath: DotSyntax
      FieldName: string
      Tokens: SyntaxToken[] }

/// <summary>Typed property assignment inside a block: `&lt;key&gt; = &lt;value&gt;` (ADR-0130).</summary>
and PropertySyntax =
    { Id: NodeId
      Span: TextSpan
      KeyPath: DotSyntax
      Key: string
      Value: PropertyValueSyntax
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
/// `Source -&gt; Target` (`producer [outPort] -&gt; [inPort] consumer`).</summary>
and FlowLinkSyntax =
    { Id: NodeId
      Span: TextSpan
      Source: FlowEndpointSyntax
      Target: FlowEndpointSyntax
      FromPath: DotSyntax
      FromNode: string
      FromPort: string option
      ToPath: DotSyntax
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

/// <summary>Target of a cell inside a bracket layout / grid row: either an empty slot `[]` or a named slot/reference.</summary>
and [<RequireQualifiedAccess>] GridCellTarget =
    | EmptySlot
    | Slot of target: DotSyntax

/// <summary>Typed cell inside a grid drawing row `[ card:weight ] [ ]` (ADR-0111 §2 / ADR-0130 / ADR-0131).</summary>
and GridCellSyntax =
    { Target: GridCellTarget
      Weight: int option }

    member this.IsEmptySlot =
        match this.Target with
        | GridCellTarget.EmptySlot -> true
        | GridCellTarget.Slot _ -> false

    member this.TargetText =
        match this.Target with
        | GridCellTarget.EmptySlot -> "[]"
        | GridCellTarget.Slot d -> d.Text

    member this.EffectiveWeight = defaultArg this.Weight 1

    member this.RawText =
        match this.Weight with
        | Some w -> $"{this.TargetText}:{w}"
        | None -> this.TargetText

/// <summary>Structural kind of a grid / layout row: either a bare `[] [] …` ruler row or a bracketed `[ a:4 [] b:8 ]` board row.</summary>
and [<RequireQualifiedAccess>] GridRowKind =
    | RulerRow of columns: int
    | BoardRow of cells: GridCellSyntax[]

/// <summary>Grid drawing row `[ card:weight ] [ ]` (ADR-0111 §2) — empty cell is
/// the whole `[]` token; cells carry structured `Kind`, `TypedCells`, and `Cells` text.</summary>
and GridRowSyntax =
    { Id: NodeId
      Span: TextSpan
      Kind: GridRowKind
      TypedCells: GridCellSyntax[]
      Cells: string[]
      Tokens: SyntaxToken[] }

/// <summary>Typed row inside a `catalog` table (`| id | title | dashspec | section | page |`).</summary>
and CatalogEntryRowSyntax =
    { Id: string
      Title: string
      DashspecPath: string
      Section: string option
      Page: string option }

/// <summary>Typed row inside a `filters` table (`| id | bind | column | label | widget | … |`).</summary>
and FilterRowSyntax =
    { Id: string
      Bind: string
      Column: string option
      Label: string option
      Widget: string option
      Attributes: Map<string, string> }

/// <summary>Typed row inside a `links` table (`| id | label | url | target | topbar | settings |`).</summary>
and LinkRowSyntax =
    { Id: string
      Label: string
      Url: string
      Target: string option
      Topbar: string option
      Settings: string option }

/// <summary>Typed row inside a `when` table (`| state | message |`).</summary>
and WhenRowSyntax =
    { State: string
      Message: string }

/// <summary>Typed row inside an `on click` table (`| source | drill | set |`).</summary>
and ClickRowSyntax =
    { Source: string
      Drill: string option
      Set: string option }

/// <summary>Typed row inside a `table` / `columns` table (`| id | format | label | sorting | alignment |`).</summary>
and ColumnRowSyntax =
    { Id: string
      Format: string option
      Label: string option
      Sorting: string option
      Alignment: string option
      HeaderAlignment: string option }

/// <summary>Domain kind of a pipe-table row inside a block (ADR-0130).</summary>
and [<RequireQualifiedAccess>] TableRowKind =
    | Header of columns: string[]
    | Separator
    | Filter of FilterRowSyntax
    | Link of LinkRowSyntax
    | When of WhenRowSyntax
    | Click of ClickRowSyntax
    | Column of ColumnRowSyntax
    | CatalogEntry of CatalogEntryRowSyntax
    | Generic of attributes: Map<string, string>

/// <summary>Markdown-style table row `| a | b |` — carries its typed domain representation (<see cref="TableRowKind"/>) and raw cells.</summary>
and TableRowSyntax =
    { Id: NodeId
      Span: TextSpan
      Kind: TableRowKind
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
