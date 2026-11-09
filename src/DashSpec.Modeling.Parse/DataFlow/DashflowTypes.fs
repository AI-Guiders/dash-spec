namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core

type SourceFromKind =
    | View
    | SqlQuery
    | SqlFile
    | Xlsx

/// <summary>Origin of a flow source (ADR-0126 §1): leaf storage constructors
/// (legacy <c>from view/sql/xlsx</c> blocks transpile into Leaf on read) or
/// source composition (<c>InnerJoin(a, b)</c>, <c>UnionAll(a, b)</c> …).
/// Composite references are data inside the source definition — not FlowLink
/// edges, not graph nodes (ADR-0126 §1.1).</summary>
type JoinCondition =
    { On: string list
      LeftKey: string list
      RightKey: string list }

type SourceOrigin =
    | Leaf of kind: SourceFromKind * value: string * sheet: string option
    | InnerJoin of left: string * right: string * cond: JoinCondition
    | LeftJoin of left: string * right: string * cond: JoinCondition
    | RightJoin of left: string * right: string * cond: JoinCondition
    | FullJoin of left: string * right: string * cond: JoinCondition
    | UnionAll of left: string * right: string

type SourceFrom =
    { Kind: SourceFromKind
      Value: string
      Sheet: string option }

/// <summary>Manifest data plugin selection on a <c>source</c> node.</summary>
type DashflowProviderBinding =
    /// Same as legacy card <c>datasource</c> — runtime <c>default_provider_id</c>.
    | Infer
    | Named of id: string

type DashflowSourceDef =
    { Id: string
      Provider: DashflowProviderBinding
      /// <summary>Where the source comes from (ADR-0126 §1): a leaf storage
      /// constructor or a composition of two other sources.</summary>
      Origin: SourceOrigin
      /// <summary>Хранение времени в источнике: "utc" | "local". Пусто — не заявлено (по умолчанию UTC).</summary>
      TimeZone: string
      OutputPort: string
      OutputRowType: string
      DefaultOutputPort: string option }

type FlowLinkDef =
    { FromNode: string
      FromPort: string option
      ToNode: string
      ToPort: string option }

type DashflowInputDecl =
    { Name: string
      PortType: DashPortType option }

type DashflowTransformParam =
    { Key: string
      Value: string }

type DashflowTransformStepDef =
    { PluginId: string
      Parameters: DashflowTransformParam[] }

/// <summary>Typed widget-value port of a filter transformer (ADR-0120):
/// <c>param event_type: string = all</c>. <c>DependsOn</c> carries the
/// cascade-domain edge (<c>depends</c> column of the filters table): the
/// option domain of this parameter is constrained by that parameter's value.
/// <c>ColumnBinding</c> (ADR-0125 §3) is the pushed-down predicate column —
/// the binding the placement resolver carries on the edge; iTVF emission
/// turns it into <c>WHERE col @param</c>.</summary>
type DashflowParamPortDef =
    { Name: string
      ValueType: string
      Default: string option
      DependsOn: string option
      ColumnBinding: string option }

type DashflowTransformerDef =
    { Id: string
      Inputs: DashflowInputDecl[]
      Outputs: (string * string)[]
      Params: DashflowParamPortDef[]
      DefaultInputPort: string option
      DefaultOutputPort: string option
      Steps: DashflowTransformStepDef[] }

/// <summary>Parent port name wired to an inner node port on a nested <c>flow</c>.</summary>
type DashflowCompositePortWire =
    { ExternalName: string
      InnerNodeId: string
      InnerPortName: string }

/// <summary>Named nested <c>flow &lt;id&gt;</c> — same body rules as <c>@flow</c> (ADR-0088).</summary>
type DashflowNestedFlowDef =
    { Id: string
      ExternalInputs: DashflowCompositePortWire[]
      ExternalOutputs: DashflowCompositePortWire[]
      Sources: DashflowSourceDef[]
      Transformers: DashflowTransformerDef[]
      Links: FlowLinkDef[]
      NestedFlows: DashflowNestedFlowDef[]
      InnerGraph: FlowGraph
      Diagnostics: FlowGraphDiagnostic[] }

type DashflowModule =
    { FlowId: string
      Sources: DashflowSourceDef[]
      Transformers: DashflowTransformerDef[]
      NestedFlows: DashflowNestedFlowDef[]
      Links: FlowLinkDef[]
      Graph: FlowGraph
      /// ADR-0127: row types synthesized by port type inference (projection
      /// steps). Merge into the document's type catalog alongside declared types.
      InferredRowTypes: RowTypeDef[]
      /// ADR-0125: the `module flow <id> view` marker — the explicit opt-in for
      /// emitting the module's chains as managed views (dsc build views).
      EmitViews: bool
      /// ADR-0125: the managed-views manifest entries for this flow (loaded from
      /// dashspec.views.json next to the corpus by the document resolver).
      ViewManifest: DashSpec.Modeling.Parse.Document.DashflowViewManifest.DashflowViewEntry list
      Diagnostics: FlowGraphDiagnostic[] }

    member this.WithSources(sources: DashflowSourceDef[]) =
        { this with Sources = sources }

    member this.WithTransformers(transformers: DashflowTransformerDef[]) =
        { this with Transformers = transformers }

    member this.WithLinks(links: FlowLinkDef[]) =
        { this with Links = links }
