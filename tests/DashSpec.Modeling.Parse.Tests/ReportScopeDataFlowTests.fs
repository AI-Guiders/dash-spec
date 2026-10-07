namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

module ReportScopeDataFlowTests =

    [<Fact>]
    let ``report data flow wires module node to card slot`` () =
        let dir = Path.Combine(Path.GetTempPath(), "report-data-flow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
@tab report_data_flow

connect
  flow "peak.dashflow"
end connect

report "Scope"
  type UtilizationRow
    string UserSam
  end type
  defaults
    filter.usage_date.range = -7d..today
  end defaults
  filter date usage_date on usage_date as "Date"

  data flow
    utilization [utilization] -> [rows] card.peak.heatmap
    drill_src [rows] -> [rows] card.peak.drill
    usage_date -> [usage_date] card.peak.heatmap
    usage_date -> [usage_date] card.peak.drill
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

end tab report_data_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]
            let heatmap = card.DiagramSlots.["heatmap"]
            Assert.True(heatmap.FlowInput.IsSome)
            Assert.Equal("utilization", heatmap.FlowInput.Value.NodeId)
            Assert.Contains("usage_date", heatmap.BoundFilters)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()