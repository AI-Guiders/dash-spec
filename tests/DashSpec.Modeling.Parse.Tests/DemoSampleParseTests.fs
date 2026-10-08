namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document

module DemoSampleParseTests =

    let private samplesDemoDir =
        Path.GetFullPath(
            Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "samples", "demo"))

    [<Fact>]
    let ``demo soak dashspec parses`` () =
        let path = Path.Combine(samplesDemoDir, "demo-soak.dashspec")
        let text = File.ReadAllText path
        let document =
            DashboardComposer.parse text (Some samplesDemoDir) DashSpecParseOptions.defaultOptions

        Assert.Equal("demo_soak", document.Id)
        Assert.Equal(18, document.Cards.Count)
        Assert.Equal(8, document.Filters.Count)
        Assert.True(document.Dashflow.IsSome)
        Assert.Equal(8, document.Dashflow.Value.Sources.Length)
        Assert.True(document.Cards |> Seq.forall (fun c -> c.FlowInput.IsSome))

    [<Fact>]
    let ``demo soak reference scan collects diagram presets`` () =
        let path = Path.Combine(samplesDemoDir, "demo-soak.dashspec")
        let text = File.ReadAllText path
        let diagramIds, _ = ReportReferenceScanner.scanModuleText text

        Assert.True(diagramIds.Contains "demo_peak_kpi")
        Assert.True(diagramIds.Contains "demo_events_detail_table")
        Assert.Equal(9, diagramIds.Count)

    [<Fact>]
    let ``demo soak link envelope scan includes tab dashspec diagram refs`` () =
        let path = Path.Combine(samplesDemoDir, "demo-soak.dashspec")
        let text = File.ReadAllText path
        let diagramIds, _ = ReportReferenceScanner.scanModuleLinkEnvelope text samplesDemoDir

        Assert.True(diagramIds.Contains "demo_period_peak_by_app_bar")
        Assert.True(diagramIds.Count > 9)

    [<Fact>]
    let ``demo soak import index fails when glob contains invalid diagram`` () =
        let diagramsDir = Path.Combine(samplesDemoDir, "diagrams")
        let junkName = "_link-test-broken-" + Guid.NewGuid().ToString("N") + ".dashdiagram"
        let junkPath = Path.Combine(diagramsDir, junkName)

        File.WriteAllText(junkPath, "@diagram link_test_broken\nTHIS IS NOT VALID\n")

        try
            let path = Path.Combine(samplesDemoDir, "demo-soak.dashspec")
            let text = File.ReadAllText path

            Assert.Throws<DashSpecParseException>(fun () ->
                DashboardComposer.parse text (Some samplesDemoDir) DashSpecParseOptions.defaultOptions
                |> ignore)
            |> ignore
        finally
            try
                File.Delete junkPath
            with _ ->
                ()
