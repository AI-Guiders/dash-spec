namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module GroupByBranchTests =

    /// ADR-0121: group_by is a graph BRANCH into a separate card port — the raw
    /// stream keeps its own slot, the grouped stream is a transformer output port
    /// with its own row type. Grammar needs no changes: `transform use group_by`
    /// is a step; the compiler owns what it means.
    let private flowText =
        """
module flow activity
    namespace Test.Flows

source events {
  use provider sqlserver
  from view demo.v_events
  ports
    default output rows
    output stream rows: EventRow
  end ports
}

transformer by_app {
  ports
    default input rows
    input stream rows: EventRow
    output stream grouped: AppCountRow
  end ports
  transform use group_by {
    by = app_name
    agg = count
  }
}

events [rows] -> [rows] by_app

end flow
"""

    [<Fact>]
    let ``group_by branch parses with typed ports and per-slot wiring`` () =
        let dir = Path.Combine(Path.GetTempPath(), "group-by-branch-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        File.WriteAllText(Path.Combine(dir, "flow.ds"), flowText)
        File.WriteAllText(
            Path.Combine(dir, "types.ds"),
            """
module types activity_types
    namespace Test.Types

type EventRow
  date usage_date
  string app_name
end type

type AppCountRow
  string app_name
  int value
end type
"""
        )

        let specText =
            """
module section group_by_branch
  import flows from Test.Flows as activity_flow
  import types from Test.Types

connect
  use flow activity_flow
end connect

report "GroupBy"
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
    events [rows] -> [rows] card.activity.raw
    by_app [grouped] -> [rows] card.activity.grouped
  end data flow

  card activity as "Activity"
  diagram ref raw table
    columns = usage_date
  end table
  view
    diagram ref grouped table
      columns = app_name
    end table
  end view
  layout
    [ raw:1 grouped:1 ]
  end layout
  end card
end report

end section group_by_branch
"""

        try
            // DashboardComposer.parse runs the full pipeline (scope flows →
            // materialize → wiring), unlike the bare section parser.
            let document = DashboardComposer.parse specText (Some dir) DashSpecParseOptions.defaultOptions
            let card = document.Cards.[0]
            Assert.Equal(2, card.DiagramSlots.Count)

            let raw = card.DiagramSlots.["raw"]
            let grouped = card.DiagramSlots.["grouped"]
            Assert.True(raw.FlowInput.IsSome)
            Assert.True(grouped.FlowInput.IsSome)
            Assert.Equal("events", raw.FlowInput.Value.NodeId)
            Assert.Equal("by_app", grouped.FlowInput.Value.NodeId)
            Assert.Equal("grouped", grouped.FlowInput.Value.PortName)

            // the grouped slot's data source carries the PORT's row type — the
            // honest output type of the first type-changing transform
            Assert.Equal("AppCountRow", grouped.DataSource.RowsType)

            // the step itself rides the IR: plugin id + parameters, no sugar
            let flow = document.Dashflow
            Assert.True(flow.IsSome)
            let transformer =
                flow.Value.Transformers |> Array.find (fun t -> t.Id = "by_app")
            let step = transformer.Steps |> Array.exactlyOne
            Assert.Equal("group_by", step.PluginId)
            Assert.Equal("app_name", step.Parameters |> Array.find (fun p -> p.Key = "by") |> fun p -> p.Value)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
