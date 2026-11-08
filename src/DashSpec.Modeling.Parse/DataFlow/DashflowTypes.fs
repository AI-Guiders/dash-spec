namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core

type SourceFromKind =
    | View
    | SqlQuery
    | SqlFile
    | Xlsx

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
      From: SourceFrom
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
/// option domain of this parameter is constrained by that parameter's value.</summary>
type DashflowParamPortDef =
    { Name: string
      ValueType: string
      Default: string option
      DependsOn: string option }

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
      Diagnostics: FlowGraphDiagnostic[] }

    member this.WithSources(sources: DashflowSourceDef[]) =
        { this with Sources = sources }

    member this.WithTransformers(transformers: DashflowTransformerDef[]) =
        { this with Transformers = transformers }
