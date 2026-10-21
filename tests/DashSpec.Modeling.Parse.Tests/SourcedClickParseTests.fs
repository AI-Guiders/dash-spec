namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse.Document

module SourcedClickParseTests =

    do GridDefaultTestBootstrap.ensure ()

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
