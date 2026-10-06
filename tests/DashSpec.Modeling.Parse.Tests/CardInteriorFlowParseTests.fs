namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

module CardInteriorFlowParseTests =

    let private flowText =
        """
@flow peak

source utilization {
  use provider sqlserver
  from view demo.v_heat
  ports
    default output utilization
    output stream utilization: UtilizationRow
  end ports
}

source drill_src {
  use provider sqlserver
  from view demo.v_drill
  ports
    default output rows
    output stream rows: UtilizationRow
  end ports
}

end flow
"""

    [<Fact>]
    let ``card flow links module ports and filters to slots`` () =
        let dir = Path.Combine(Path.GetTempPath(), "card-interior-flow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            """
@tab card_interior_flow

connect
  flow "peak.dashflow"
end connect

report "Interior"
  type UtilizationRow
    string UserSam
  end type
  defaults
    filter.usage_date.range = -7d..today
  end defaults
  filter date usage_date on usage_date as "Date"
  filter field app_name on dbo.t.app as "App"
  card peak as "Peak"
  diagram ref drill table
    columns = UserSam
  end table
  flow
    utilization [utilization] -> [rows] heatmap
    drill_src [rows] -> [rows] drill
    usage_date -> [usage_date] heatmap
    app_name -> [app_name] heatmap
    usage_date -> [usage_date] drill
    app_name -> [app_name] drill
  end flow
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

end tab card_interior_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]
            Assert.True(card.InteriorFlow.IsSome)
            Assert.Equal(6, card.InteriorFlow.Value.Links.Count)
            let heatmap = card.DiagramSlots.["heatmap"]
            let drill = card.DiagramSlots.["drill"]
            Assert.Equal("utilization", heatmap.FlowInput.Value.NodeId)
            Assert.Equal("drill_src", drill.FlowInput.Value.NodeId)
            Assert.Equal<string list>([ "usage_date"; "app_name" ], heatmap.BoundFilters |> Seq.toList)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
