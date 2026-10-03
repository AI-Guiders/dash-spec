namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core

type SourceFromKind =
    | View
    | SqlQuery
    | SqlFile

type SourceFrom =
    { Kind: SourceFromKind
      Value: string }

type DashflowSourceDef =
    { Id: string
      ProviderId: string option
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
