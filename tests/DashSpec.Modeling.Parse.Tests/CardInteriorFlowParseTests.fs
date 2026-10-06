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
    let ``card flow wires inputs to slots and bind`` () =
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
  card peak as "Peak"
  diagram ref drill table
    columns = UserSam
  end table
  input heatmap from utilization.utilization
  input drill from drill_src.rows
  flow
    bind heatmap usage_date
    bind drill usage_date
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
            Assert.Equal(2, card.CardInputs.Count)
            Assert.True(card.InteriorFlow.IsSome)
            Assert.Empty(card.InteriorFlow.Value.Links)
            Assert.Equal(2, card.InteriorFlow.Value.SlotBinds.Count)
            let heatmap = card.DiagramSlots.["heatmap"]
            let drill = card.DiagramSlots.["drill"]
            Assert.Equal("utilization", heatmap.FlowInput.Value.NodeId)
            Assert.Equal("drill_src", drill.FlowInput.Value.NodeId)
            Assert.Equal<string list>([ "usage_date" ], heatmap.BoundFilters |> Seq.toList)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
