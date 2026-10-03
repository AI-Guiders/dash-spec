namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core

type SourceFromKind =
    | View
    | SqlQuery
    | SqlFile

type SourceFrom =
    { Kind: SourceFromKind
      Value: string }

/// <summary>Manifest data plugin selection on a <c>source</c> node.</summary>
type DashflowProviderBinding =
    /// Same as legacy card <c>datasource</c> — runtime <c>default_provider_id</c>.
    | Infer
    | Named of id: string

type DashflowSourceDef =
    { Id: string
      Provider: DashflowProviderBinding
      From: SourceFrom
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

type DashflowTransformerDef =
    { Id: string
      Inputs: DashflowInputDecl[]
      Outputs: (string * string)[]
      DefaultInputPort: string option
      DefaultOutputPort: string option }

type DashflowModule =
    { FlowId: string
      Sources: DashflowSourceDef[]
      Transformers: DashflowTransformerDef[]
      Links: FlowLinkDef[]
      Graph: FlowGraph
      Diagnostics: FlowGraphDiagnostic[] }
