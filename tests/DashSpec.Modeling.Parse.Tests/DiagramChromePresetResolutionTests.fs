namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document

[<Xunit.Collection("GridDefault")>]
module DiagramChromePresetResolutionTests =

    /// The compiler resolves `chrome use <preset>` on a diagram module at unit
    /// registration — in one place, before any consumer. The registered fragment
    /// carries the effective property bag (preset values with the module's explicit
    /// chrome as overrides) and no dangling UsePreset string.
    [<Fact>]
    let ``diagram chrome use resolves into effective properties`` () =
        let dir = Path.Combine(Path.GetTempPath(), "chrome-preset-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore

        File.WriteAllText(
            Path.Combine(dir, "pres.ds"),
            [
                "module presentation tall_preset"
                "namespace Test.Pres"
                ""
                "  height = 420"
                "  visible_rows = 8"
                "  align = center"
            ]
            |> String.concat "\n"
        )

        File.WriteAllText(
            Path.Combine(dir, "diag.ds"),
            [
                "module diagram tall_diag"
                "namespace Test.Diags"
                ""
                "chrome"
                "  use tall_preset"
                "  stretch_mode = fill"
                "end chrome"
                ""
                "heatmap"
                "  x = d as \"D\""
                "  y = a as \"A\""
                "  value = v as \"V\""
                "end heatmap"
            ]
            |> String.concat "\n"
        )

        let specText =
            [
                "module section preset_flow"
                "  import diagrams from Test.Diags"
                "  import presentations from Test.Pres"
                ""
                "report \"Presets\""
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
                "    diagram ref heatmap tall_diag"
                "  end view"
                "  layout"
                "    [ heatmap ]"
                "    [ drill ]"
                "  end layout"
                "  end card"
                "end report"
                ""
                "end section preset_flow"
            ]
            |> String.concat "\n"

        try
            let document = DashboardComposer.parse specText (Some dir) DashSpecParseOptions.defaultOptions
            let card = document.Cards |> Seq.find (fun c -> c.Id = "c")

            // chrome of a diagram module folds into the fragment presentation —
            // the exact block the runtime absorbs (ChartChromeProperties). After
            // compile-time resolution it carries the effective bag: preset values
            // with the module's explicit chrome as overrides, no UsePreset string.
            let chrome =
                match card.Presentation with
                | Some block -> block
                | None -> failwith "card presentation (module chrome) is missing"

            let dump (b: DashSpec.Modeling.Parse.Presentation.PresentationBlock) =
                let props =
                    b.Properties
                    |> Seq.map (fun kv -> $"{kv.Key}={kv.Value}")
                    |> String.concat "; "

                $"use={b.UsePreset} props=[{props}]"

            Assert.True(chrome.UsePreset.IsNone, $"chrome use must not dangle — {dump chrome}")
            Assert.Equal("center", chrome.Properties.["align"])
            Assert.Equal("420", chrome.Properties.["height"])
            Assert.Equal("fill", chrome.Properties.["stretch_mode"])
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()

    /// A `chrome use` naming an unregistered preset is a loud compile error —
    /// the renderer never guesses and the runtime never silently drops the knob.
    [<Fact>]
    let ``unknown chrome preset fails loudly`` () =
        let dir = Path.Combine(Path.GetTempPath(), "chrome-preset-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore

        File.WriteAllText(
            Path.Combine(dir, "diag.ds"),
            [
                "module diagram orphan_diag"
                "namespace Test.Diags"
                ""
                "chrome"
                "  use no_such_preset"
                "end chrome"
                ""
                "heatmap"
                "  x = d as \"D\""
                "  y = a as \"A\""
                "  value = v as \"V\""
                "end heatmap"
            ]
            |> String.concat "\n"
        )

        let specText =
            [
                "module section orphan_flow"
                "  import diagrams from Test.Diags"
                ""
                "report \"Orphan\""
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
                "    diagram ref heatmap orphan_diag"
                "  end view"
                "  layout"
                "    [ heatmap ]"
                "    [ drill ]"
                "  end layout"
                "  end card"
                "end report"
                ""
                "end section orphan_flow"
            ]
            |> String.concat "\n"

        try
            let ex =
                Assert.Throws<DashSpecParseException>(fun () ->
                    DashboardComposer.parse specText (Some dir) DashSpecParseOptions.defaultOptions
                    |> ignore
                )

            Assert.Contains("no_such_preset", ex.Message)
        finally
            try
                Directory.Delete(dir, true)
            with _ ->
                ()
