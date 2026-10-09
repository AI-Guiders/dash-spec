namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Document

module ReportScopeWiringGraphTests =

    [<Fact>]
    let ``parse exposes wiring graph with report scope flow and card interior links`` () =
        let dir = Path.Combine(Path.GetTempPath(), "report-scope-flow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), CardInteriorFlowParseTests.flowTextForReuse)

        let specText =
            """
@tab report_scope_flow

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
    app_name -> report
  end placement flow

  card peak as "Peak"
  diagram ref drill table
    columns = UserSam
  end table
  data flow
    utilization [utilization] -> [rows] heatmap
    drill_src [rows] -> [rows] drill
    usage_date -> [usage_date] heatmap
    app_name -> [app_name] heatmap
    usage_date -> [usage_date] drill
    app_name -> [app_name] drill
  end data flow
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

end tab report_scope_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            Assert.True(document.WiringGraph.Edges.Length > 0)

            Assert.True(
                document.WiringGraph.Edges
                |> Array.exists (fun e ->
                    e.Kind = WiringEdgeKind.Route
                    && e.From = "usage_date"
                    && e.To = "chrome.dashboard")
            )

            Assert.True(
                document.WiringGraph.Edges
                |> Array.exists (fun e -> e.Kind = WiringEdgeKind.Flow && e.To = "heatmap")
            )
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
