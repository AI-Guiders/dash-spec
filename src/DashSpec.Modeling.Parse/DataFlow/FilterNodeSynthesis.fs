namespace DashSpec.Modeling.Parse.DataFlow

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Filter

/// <summary>
/// ADR-0120 sugar→IR: each <c>FilterDefinition</c> transpiles into a typed
/// transformer node with a param port — the graph-era representation of a filter.
/// Pure synthesis; wiring into report/card flows is the placement resolver's job.
/// </summary>
module FilterNodeSynthesis =

    /// <summary>ADR-0120 compile artifact: synthesized filter param nodes plus
    /// the re-routed Data edges (producer → filters in bind order → card
    /// anchor). Additive IR — legacy consumers unaffected.</summary>
    [<CLIMutable>]
    type FilterFlowPlan =
        { Nodes: DashflowTransformerDef[]
          Edges: FlowLinkDef[] }

    /// Where the synthesized node lives: report flow (one state, all consumers
    /// below) or card flow (own instance, own input stream).
    [<RequireQualifiedAccess>]
    type FilterNodeScope =
        | Report
        | Card of cardId: string

    /// Param value type by filter kind (ADR-0120: date stops being a special
    /// kind — daterange is just another scalar parameter type).
    let paramValueType (kind: FilterKind) =
        match kind with
        | FilterKind.Date -> "daterange"
        | FilterKind.Field -> "string"
        | FilterKind.Top -> "int"
        | other -> raise (DashSpecParseException($"Unknown filter kind '{other}'."))

    /// <summary>Node id for a filter: <c>filter.&lt;name&gt;</c> on report scope,
    /// <c>card.&lt;cardId&gt;.filter.&lt;name&gt;</c> on card scope — same id grammar
    /// as other flow nodes, so edges reference filters uniformly.</summary>
    let nodeId (scope: FilterNodeScope) (filterName: string) =
        match scope with
        | FilterNodeScope.Report -> $"filter.{filterName}"
        | FilterNodeScope.Card cardId -> $"card.{cardId}.filter.{filterName}"

    /// Synthesize the transformer def: one param port (name, type, default from the
    /// author's default expression, DependsOn from the filters-table column).
    /// Row ports stay untyped here — the column binding rides the input edge and is
    /// type-checked by the graph compiler (ADR-0079), not by sugar.
    let synthesize (scope: FilterNodeScope) (filter: FilterDefinition) : DashflowTransformerDef =
        { Id = nodeId scope filter.Name
          Inputs = [| { Name = "rows"; PortType = None } |]
          Outputs = [| ("rows", "") |]
          Params =
            [| { Name = filter.Name
                 ValueType = paramValueType filter.Kind
                 Default = filter.DefaultExpression
                 DependsOn = filter.DependsOn
                 ColumnBinding = filter.ColumnReference } |]
          DefaultInputPort = Some "rows"
          DefaultOutputPort = Some "rows"
          Steps = [||] }

    /// Synthesize all report-scope filters of a document shell.
    let synthesizeReportScope (filters: FilterDefinition seq) =
        filters |> Seq.map (synthesize FilterNodeScope.Report) |> Array.ofSeq

    /// Synthesize the card-scope filters of one card (own instance per card).
    let synthesizeCardScope (cardId: string) (filters: FilterDefinition seq) =
        filters |> Seq.map (synthesize (FilterNodeScope.Card cardId)) |> Array.ofSeq

    /// <summary>ADR-0120: the author's <c>series max = N</c> sugar becomes a
    /// <c>reduce</c> transform instance on the card path (no special node kind —
    /// the tail is just the transform's <c>tail</c> parameter). Options tap before
    /// it (full domain), the matrix consumes its output ("as on the diagram").</summary>
    let synthesizeReduce
        (cardId: string)
        (max: int)
        (otherLabel: string option)
        : DashflowTransformerDef =
        let parameters =
            [ { DashflowTransformParam.Key = "by"; DashflowTransformParam.Value = "series" }
              { DashflowTransformParam.Key = "top"; DashflowTransformParam.Value = string max } ]
            @ (match otherLabel with
               | Some label -> [ { DashflowTransformParam.Key = "tail"; DashflowTransformParam.Value = label } ]
               | None -> [])
            |> Array.ofList

        { Id = $"card.{cardId}.reduce"
          Inputs = [| { Name = "rows"; PortType = None } |]
          Outputs = [| ("rows", "") |]
          Params = [||]
          DefaultInputPort = Some "rows"
          DefaultOutputPort = Some "rows"
          Steps = [| { PluginId = "reduce"; Parameters = parameters } |] }
