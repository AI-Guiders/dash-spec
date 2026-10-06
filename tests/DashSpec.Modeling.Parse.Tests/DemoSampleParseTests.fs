namespace DashSpec.Modeling.Parse.Tests

open System
open System.IO
open Xunit
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
