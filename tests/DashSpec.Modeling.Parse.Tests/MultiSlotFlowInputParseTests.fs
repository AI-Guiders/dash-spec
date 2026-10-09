namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

module MultiSlotFlowInputParseTests =

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
    let ``report data flow binds module outputs per diagram slot`` () =
        let dir = Path.Combine(Path.GetTempPath(), "multi-slot-flow-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "peak.dashflow"), flowText)

        let specText =
            """
@tab multi_slot_flow

connect
  flow "peak.dashflow"
end connect

report "Multi"
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

end tab multi_slot_flow
"""

        try
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]
            Assert.Equal(2, card.DiagramSlots.Count)
            let heatmap = card.DiagramSlots.["heatmap"]
            let drill = card.DiagramSlots.["drill"]
            Assert.True(heatmap.FlowInput.IsSome)
            Assert.True(drill.FlowInput.IsSome)
            Assert.Equal("utilization", heatmap.FlowInput.Value.NodeId)
            Assert.Equal("drill_src", drill.FlowInput.Value.NodeId)
            Assert.Equal("rows", drill.FlowInput.Value.PortName)
            Assert.True(card.FlowInput.IsSome)
            Assert.Equal("utilization", card.FlowInput.Value.NodeId)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()