namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module SourcedClickParseTests =


    let private specText =
        """
module tab sourced_click
  import diagrams from Test.Diagrams

report "Sourced clicks"
  filters
    | id       | bind  | column   | label | widget | ref | range  |
    | app_name | field | app_name | App   | -      | -   | -      |
    | the_day  | date  | the_day  | Day   | -      | -   | -7d..today |
  end filters

  card act ref act
    title = "Act"
    on click
      | source        | drill     | set                 |
      | cell          | drill     | app_name=y          |
      | category item | app_drill | app_name=y          |
    end click
    view
      diagram ref heatmap hm
    end view
    diagram ref drill cell_drill
    diagram ref app_drill app_drill
    data flow
      hm_flow [rows] -> [rows] heatmap
      cell_flow [rows] -> [rows] drill
      app_flow [rows] -> [rows] app_drill
      app_name -> [app_name] heatmap
      app_name -> [app_name] drill
      app_name -> [app_name] app_drill
      the_day -> [the_day] heatmap
      the_day -> [the_day] drill
      the_day -> [the_day] app_drill
    end data flow
    layout
      [ heatmap ]
      [ drill ]
      [ app_drill ]
    end layout
  end card
end report

end tab sourced_click
"""

    let private writeTempDiagram (dir: string) =
        let diagramText =
            """
module diagram hm
namespace Test.Diagrams

heatmap
  x = hour
  y = app_name
end heatmap
"""
        File.WriteAllText(Path.Combine(dir, "hm.ds"), diagramText)

    [<Fact>]
    let ``click table desugars into sourced handlers with drill slots`` () =
        let dir = Path.Combine(Path.GetTempPath(), "sourced-click-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeTempDiagram dir
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]

            Assert.Equal(2, card.ClickHandlers.Count)

            let cell = card.ClickHandlers.[0]
            Assert.Equal("cell", cell.Source)
            let cellDrill = cell.Effects.[0]
            Assert.Equal("drill", cellDrill.TryGetDrillTarget.Value)

            let category = card.ClickHandlers.[1]
            Assert.Equal("category item", category.Source)
            let appDrill = category.Effects.[0]
            Assert.Equal("app_drill", appDrill.TryGetDrillTarget.Value)
            let binds = appDrill.TryGetDrillBinds.Value
            Assert.Equal(1, binds.Count)
            Assert.Equal("app_name", fst binds.[0])
            Assert.Equal("y", snd binds.[0])
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    [<Fact>]
    let ``route link binds app_name into the app_drill slot bound filters`` () =
        let dir = Path.Combine(Path.GetTempPath(), "sourced-click-bind-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeTempDiagram dir
            let document = DocumentModuleParser.parseDocumentDefault specText (Some dir)
            let card = document.Cards.[0]

            let slots = card.DiagramSlots
            Assert.True(slots.ContainsKey("app_drill"))
            Assert.Contains("app_name", slots.["app_drill"].BoundFilters)
            Assert.Contains("the_day", slots.["app_drill"].BoundFilters)
            Assert.Contains("app_name", slots.["drill"].BoundFilters)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    /// ADR-0113: `diagram ref <slot>` + `payload` lines, dotted click targets and dotted flow links.
    let private switchSlotSpec =
        """
module tab switch_slot
  import diagrams from Test.Diagrams

report "Switch slot"
  filters
    | id       | bind  | column   | label | widget | ref | range  |
    | app_name | field | app_name | App   | -      | -   | -      |
    | the_day  | date  | the_day  | Day   | -      | -   | -7d..today |
  end filters

  card act ref act
    title = "Act"
    on click
      | source        | drill             | set        |
      | cell          | drill.peak_table  | app_name=y |
      | category item | drill.users_table | app_name=y |
    end click
    view
      diagram ref heatmap hm
    end view
    diagram ref drill
      payload peak_table  cell_drill
      payload users_table app_drill
    data flow
      hm_flow [rows] -> [rows] heatmap
      cell_flow [rows] -> [rows] drill.peak_table
      app_flow [rows] -> [rows] drill.users_table
      app_name -> [app_name] heatmap
      app_name -> [app_name] drill
      the_day -> [the_day] heatmap
      the_day -> [the_day] drill
    end data flow
    layout
      [ heatmap drill ]
    end layout
  end card
end report

end tab switch_slot
"""

    [<Fact>]
    let ``switch slot parses payloads with dotted click and flow targets`` () =
        let dir = Path.Combine(Path.GetTempPath(), "switch-slot-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        try
            writeTempDiagram dir
            let document = DocumentModuleParser.parseDocumentDefault switchSlotSpec (Some dir)
            let card = document.Cards.[0]

            Assert.True(card.DiagramSlots.ContainsKey("drill"))
            Assert.False(card.DiagramSlots.ContainsKey("app_drill"))
            let slot = card.DiagramSlots.["drill"]
            Assert.Equal(2, slot.Payloads.Count)
            Assert.Equal("peak_table", slot.Payloads.[0].Name)
            Assert.Equal("users_table", slot.Payloads.[1].Name)
            Assert.Equal("cell_drill", slot.Payloads.[0].Diagram.UsePreset.Value)
            Assert.Equal("app_drill", slot.Payloads.[1].Diagram.UsePreset.Value)
            // The head payload feeds the classic Diagram field (kind resolution, awaiting state).
            Assert.Equal("cell_drill", slot.Diagram.UsePreset.Value)
            // Per-payload flow inputs; slot-level routes land in BoundFilters for both.
            Assert.Equal("cell_flow", slot.Payloads.[0].FlowInput.Value.NodeId)
            Assert.Equal("app_flow", slot.Payloads.[1].FlowInput.Value.NodeId)
            Assert.Contains("app_name", slot.BoundFilters)
            Assert.Contains("the_day", slot.BoundFilters)
            // Dotted click targets keep slot.payload addressing.
            Assert.Equal("drill.peak_table", card.ClickHandlers.[0].Effects.[0].TryGetDrillTarget.Value)
            Assert.Equal("drill.users_table", card.ClickHandlers.[1].Effects.[0].TryGetDrillTarget.Value)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
