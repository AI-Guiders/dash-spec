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
      From: SourceFrom
      OutputPort: string
      OutputRowType: string }

type DashflowTransformerDef =
    { Id: string
      Inputs: (string * FlowNodePortRef)[]
      Outputs: (string * string)[] }

type DashflowModule =
    { FlowId: string
      Sources: DashflowSourceDef[]
      Transformers: DashflowTransformerDef[]
      Graph: FlowGraph
      Diagnostics: FlowGraphDiagnostic[] }
