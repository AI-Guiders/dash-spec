namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module ImportOrderIndependenceTests =

    /// The compiler owns the resolution order: a diagram folding `chrome use
    /// <preset>` resolves even when the preset's import directive comes AFTER the
    /// diagram's (or only implicitly, at project level). Author's file order must
    /// not decide visibility.
    [<Fact>]
    let ``diagram resolves a preset imported after it`` () =
        let dir = Path.Combine(Path.GetTempPath(), "import-order-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore

        let presText =
            [ "module presentation late_preset"
              "namespace Test.Pres"
              ""
              "  height = 400"
              "  align = center" ]
            |> String.concat "\n"

        let diagText =
            [ "module diagram late_diag"
              "namespace Test.Diags"
              ""
              "chrome"
              "  use late_preset"
              "end chrome"
              ""
              "heatmap"
              "  x = d as \"D\""
              "  y = a as \"A\""
              "  value = v as \"V\""
              "end heatmap" ]
            |> String.concat "\n"

        File.WriteAllText(Path.Combine(dir, "pres.ds"), presText)
        File.WriteAllText(Path.Combine(dir, "diag.ds"), diagText)

        // diagrams imported FIRST, presentation after — the order that used to
        // silently drop the preset
        let specLines =
            [ "module section order_flow"
              "  import diagrams from Test.Diags"
              "  import presentations from Test.Pres"
              ""
              "report \"Order\""
              "  type R"
              "    date d"
              "    string a"
              "    decimal v"
              "  end type"
              "  defaults"
              "    filter.d.range = -7d..today"
              "  end defaults"
              "  filter d"
              "    bind date"
              "      column = d"
              "    end bind"
              "    show"
              "      label = \"D\""
              "    end show"
              "  end filter"
              ""
              "  data flow"
              "    src [rows] -> [rows] card.c.heatmap"
              "    src2 [rows] -> [rows] card.c.drill"
              "  end data flow"
              ""
              "  card c as \"C\""
              "  diagram ref drill table"
              "    columns = d"
              "  end table"
              "  view"
              "    diagram ref heatmap late_diag"
              "  end view"
              "  layout"
              "    [ heatmap ]"
              "    [ drill ]"
              "  end layout"
              "  end card"
              "end report"
              ""
              "end section order_flow" ]

        let specText = specLines |> String.concat "\n"

        try
            let document = DashboardComposer.parse specText (Some dir) DashSpecParseOptions.defaultOptions
            let registered = document.ResolvedChartChromePresets
            Assert.True(registered.ContainsKey("late_preset"), "preset imported after the diagram never registered")
            let preset = registered.["late_preset"]
            Assert.True(preset.Properties.ContainsKey("align"))
            Assert.Equal("center", preset.Properties.["align"])
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
