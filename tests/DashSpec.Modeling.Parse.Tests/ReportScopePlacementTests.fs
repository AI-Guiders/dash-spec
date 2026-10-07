namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Document

module ReportScopePlacementTests =

    [<Fact>]
    let ``placement flow wires filters to report and card`` () =
        let dir = Path.Combine(Path.GetTempPath(), "placement-flow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
@tab placement_flow

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
  filter field app_name on dbo.t.app as "App"

  placement flow
    usage_date -> report
    app_name -> card.peak
  end placement flow

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

end tab placement_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            Assert.Contains("usage_date", document.DashboardFilters)
            let card = document.Cards.[0]
            Assert.Contains("app_name", card.LocalFilters)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()