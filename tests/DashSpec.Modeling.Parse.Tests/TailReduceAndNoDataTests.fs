namespace DashSpec.Modeling.Parse.Tests

open System
open System.Collections.Generic
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module TailReduceAndNoDataTests =

    /// ADR-0120: `series max` sugar synthesizes a reduce transform instance and the card
    /// chain routes through it (last filter → reduce → anchor; options tap
    /// before it, the matrix consumes its output).
    [<Fact>]
    let ``tail reduce node synthesizes from series max sugar with tail label`` () =
        let node =
            FilterNodeSynthesis.synthesizeReduce "peak" 12 (Some "Другие (N)")

        Assert.Equal("card.peak.reduce", node.Id)
        Assert.Single(node.Steps) |> ignore
        let step = node.Steps.[0]
        Assert.Equal("reduce", step.PluginId)
        let byKey k = step.Parameters |> Array.find (fun p -> p.Key = k) |> fun p -> p.Value
        Assert.Equal("series", byKey "by")
        Assert.Equal("12", byKey "top")
        Assert.Equal("Другие (N)", byKey "tail")

    [<Fact>]
    let ``tail reduce without other label carries no tail parameter`` () =
        let node = FilterNodeSynthesis.synthesizeReduce "peak" 5 None
        let step = node.Steps.[0]
        Assert.Equal(2, step.Parameters.Length) // by + top only

    [<Fact>]
    let ``card chain routes through the tail reduce node`` () =
        let dir = Path.Combine(Path.GetTempPath(), "tailreduce-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.ds"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
module section tailreduce_flow
  import flows from Test.Flows as peak_flow

connect
  use flow peak_flow
end connect

report "TR"
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

  placement flow
    usage_date -> report
  end placement flow

  data flow
    utilization [utilization] -> [rows] card.peak.heatmap
    drill_src [rows] -> [rows] card.peak.drill
  end data flow

  card peak as "Peak"
  bind usage_date
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

end section tailreduce_flow
"""

        try
            // series lives in diagram modules; the card-side sugar is the override
            // block — patch the parsed card to carry it, then synthesize directly
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]
            let patched = card.WithSeriesTransform(Some { UsePreset = None; Max = Some 12; OtherLabel = Some "Другие" })
            let patchedDoc = { document with Cards = [| patched |] :> IReadOnlyList<CardDefinition> }

            let plan = FilterFlowSynthesis.synthesize patchedDoc

            let nodeIds = plan.Nodes |> Array.map (fun n -> n.Id) |> Set.ofArray
            Assert.Contains("card.peak.reduce", nodeIds)

            let edges = plan.Edges |> Array.map (fun e -> $"{e.FromNode} -> {e.ToNode}")
            Assert.Contains("filter.usage_date -> card.peak.reduce", edges)
            Assert.Contains("card.peak.reduce -> card.peak.heatmap", edges)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    /// ADR-0120: `when <filter>.no_data { message }` joins the when family.
    [<Fact>]
    let ``when no_data parses into the when family with message`` () =
        let dir = Path.Combine(Path.GetTempPath(), "nodata-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.ds"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
module section nodata_flow
  import flows from Test.Flows as peak_flow

connect
  use flow peak_flow
end connect

report "ND"
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
  when usage_date.no_data
    message = "Нет данных в выбранном периоде"
  end when
  end card
end report

end section nodata_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]
            match card.Visibility with
            | Some r ->
                Assert.Equal("usage_date", r.FilterName)
                Assert.Equal(CardVisibilityMode.WhenNoData, r.Mode)
                Assert.Equal(Some "Нет данных в выбранном периоде", r.Message)
            | None -> failwith "visibility rule not parsed"
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    [<Fact>]
    let ``when dotted state other than no_data is a loud error`` () =
        let dir = Path.Combine(Path.GetTempPath(), "nodata-bad-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.ds"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
module section nodata_bad
  import flows from Test.Flows as peak_flow

connect
  use flow peak_flow
end connect

report "NDBad"
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
  when usage_date.mystery
  end when
  end card
end report

end section nodata_bad
"""

        try
            let ex = Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () ->
                DocumentModuleParser.parseDocumentDefault specText (Some dir) |> ignore)
            Assert.Contains("no_data", ex.Message)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
