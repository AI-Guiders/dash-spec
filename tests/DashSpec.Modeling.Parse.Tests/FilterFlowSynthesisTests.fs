namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module FilterFlowSynthesisTests =

    /// ADR-0120 compile phase: bound filters transpile into param nodes
    /// (scope by placement) and card data edges re-route through them.
    [<Fact>]
    let ``filter flow plan synthesizes nodes and routes card edges through them`` () =
        let dir = Path.Combine(Path.GetTempPath(), "filterflow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.ds"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
module section filterflow_flow
  import flows from Test.Flows as peak_flow

connect
  use flow peak_flow
end connect

report "FF"
  type UtilizationRow
    string UserSam
  end type
  defaults
    filter.usage_date.range = -7d..today
  end defaults
  filter usage_date
    bind date
      column = usage_date
    end bind
    show
      label = "Date"
    end show
  end filter
  filter app_name
    bind field
      column = dbo.t.app
    end bind
    show
      label = "App"
    end show
  end filter

  placement flow
    usage_date -> report
    app_name -> card.peak
  end placement flow

  data flow
    utilization [utilization] -> [rows] card.peak.heatmap
    drill_src [rows] -> [rows] card.peak.drill
  end data flow

  card peak as "Peak"
  bind app_name, usage_date
  diagram ref drill table
    columns = UserSam
  end table
  view
    diagram ref heatmap table
      columns = UserSam
    end table
  end view
  layout
    [ heatmap ]
    [ drill ]
  end layout
  end card
end report

end section filterflow_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let plan =
                match document.FilterFlowPlan with
                | Some p -> p
                | None -> failwith "FilterFlowPlan was not synthesized"

            let nodeIds = plan.Nodes |> Array.map (fun n -> n.Id) |> Set.ofArray
            Assert.Contains("filter.usage_date", nodeIds)          // report scope (placement -> report)
            Assert.Contains("card.peak.filter.app_name", nodeIds)  // card scope (placement -> card.peak)

            // data edge chain: producer -> filters in bind order -> card anchor
            let edges = plan.Edges |> Array.map (fun e -> $"{e.FromNode} -> {e.ToNode}")
            Assert.Contains("utilization -> card.peak.filter.app_name", edges)
            Assert.Contains("card.peak.filter.app_name -> filter.usage_date", edges)
            Assert.Contains("filter.usage_date -> card.peak.heatmap", edges)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    /// Loud error: a depends target that is not declared anywhere.
    [<Fact>]
    let ``unknown depends target fails the document compile loudly`` () =
        let dir = Path.Combine(Path.GetTempPath(), "filterflow-ghost-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.ds"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
module section filterflow_ghost
  import flows from Test.Flows as peak_flow

connect
  use flow peak_flow
end connect

report "Ghost"
  type UtilizationRow
    string UserSam
  end type
  defaults
    filter.usage_date.range = -7d..today
  end defaults
  filters
    | id         | bind  | column    | label   | widget   | range      | depends   |
    | usage_date | date  | usage_date | Период | range    | -7d..today | -         |
    | app_name   | field | dbo.t.app | Продукт | combobox | -          | nowhere   |
  end filters

  data flow
    utilization [utilization] -> [rows] card.peak.heatmap
    drill_src [rows] -> [rows] card.peak.drill
  end data flow

  card peak as "Peak"
  diagram ref drill table
    columns = UserSam
  end table
  view
    diagram ref heatmap table
      columns = UserSam
    end table
  end view
  layout
    [ heatmap ]
    [ drill ]
  end layout
  end card
end report

end section filterflow_ghost
"""

        try
            let ex = Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () ->
                DocumentModuleParser.parseDocumentDefault specText (Some dir) |> ignore)
            Assert.Contains("depends on 'nowhere'", ex.Message)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
