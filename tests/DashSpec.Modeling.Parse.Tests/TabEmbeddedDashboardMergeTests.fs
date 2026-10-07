namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document

module TabEmbeddedDashboardMergeTests =

    let private flowText =
        """
@flow embed_flow

source mod {
  use provider sqlserver
  from view demo.v_heat
  ports
    default output rows
    output stream rows: HeatRow
  end ports
}

end flow
"""

    [<Fact>]
    let ``dashboard merge parses tab multi-slot card when filters are report-level`` () =
        let dir = Path.Combine(Path.GetTempPath(), "tab-embed-merge-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "embed.dashflow"), flowText)

        let tabText =
            """
@tab versions

connect
  flow "embed.dashflow"
end connect

report "Versions"
  defaults
    filter.versions_usage_date.range = -7d..today
  end defaults

  filter versions_usage_date
    bind date
      column = usage_date
    end bind
    show
      label = "Date"
    end show
  end filter

  card peak as "Peak"
  view
    diagram ref heatmap table
      columns = UserSam
    end table
  end view
  data flow
    mod [rows] -> [rows] heatmap
    versions_usage_date -> [versions_usage_date] heatmap
  end data flow
  layout
    [ heatmap ]
  end layout
  end card
end report

end tab versions
"""

        let dashboardText =
            """
@dashboard soak

connect
  flow "embed.dashflow"
end connect

report "Soak"
  defaults
    filter.usage_date.range = -7d..today
  end defaults

  filter usage_date
    bind date
      column = usage_date
    end bind
    show
      label = "Parent date"
    end show
  end filter

  tab versions as "Versions" dashspec "tab-versions.dashspec"
end report
"""

        File.WriteAllText(Path.Combine(dir, "tab-versions.dashspec"), tabText)

        try
            let document =
                DashboardComposer.parse dashboardText (Some dir) DashSpecParseOptions.defaultOptions

            let card = document.Cards |> Seq.find (fun c -> c.Id = "peak")
            Assert.True(card.DiagramSlots.ContainsKey "heatmap")
            let heatmap = card.DiagramSlots.["heatmap"]
            Assert.True(heatmap.FlowInput.IsSome)
            Assert.Equal("mod", heatmap.FlowInput.Value.NodeId)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    [<Fact>]
    let ``dashboard merge fails when embed tab keeps filters only in standalone`` () =
        let dir = Path.Combine(Path.GetTempPath(), "tab-embed-standalone-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "embed.dashflow"), flowText)

        let tabText =
            """
@tab versions

connect
  flow "embed.dashflow"
end connect

report "Versions"
  standalone
    defaults
      filter.versions_usage_date.range = -7d..today
    end defaults

    filter versions_usage_date
      bind date
        column = usage_date
      end bind
      show
        label = "Date"
      end show
    end filter
  end standalone

  card peak as "Peak"
  view
    diagram ref heatmap table
      columns = UserSam
    end table
  end view
  data flow
    mod [rows] -> [rows] heatmap
    versions_usage_date -> [versions_usage_date] heatmap
  end data flow
  layout
    [ heatmap ]
  end layout
  end card
end report

end tab versions
"""

        let dashboardText =
            """
@dashboard soak

connect
  flow "embed.dashflow"
end connect

report "Soak"
  tab versions as "Versions" dashspec "tab-versions.dashspec"
end report
"""

        File.WriteAllText(Path.Combine(dir, "tab-versions.dashspec"), tabText)

        try
            let ex =
                Assert.Throws<DashSpec.Modeling.Core.DashSpecParseException>(fun () ->
                    DashboardComposer.parse dashboardText (Some dir) DashSpecParseOptions.defaultOptions |> ignore)

            Assert.True(
                ex.Message.Contains("more than one flow input", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already has a flow input", StringComparison.OrdinalIgnoreCase),
                ex.Message)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
