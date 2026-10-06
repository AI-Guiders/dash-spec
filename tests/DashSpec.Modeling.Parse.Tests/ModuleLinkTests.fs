namespace DashSpec.Modeling.Parse.Tests

open System
open System.Collections.Generic
open System.IO
open Xunit
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Include

module ModuleLinkTests =

    let private write (dir: string) (name: string) (content: string) =
        File.WriteAllText(Path.Combine(dir, name), content)

    [<Fact>]
    let ``MembershipUnion glob skips unreferenced broken diagram`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashspec-link-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore

        write dir "good.dashdiagram" "@diagram used_heatmap\nheatmap { }\n"
        write dir "bad.dashdiagram" "@diagram broken\nTHIS IS NOT VALID\n"

        let state = ModuleIncludeState()
        let refs = HashSet<string>([ "used_heatmap" ], StringComparer.OrdinalIgnoreCase) :> ISet<_>

        let options =
            { DashSpecParseOptions.defaultOptions with
                LinkOnlyReferencedDiagramUnits = true }

        IncludeExpander.linkEnvelope
            [ ModuleLinkDirective.PathReference "*.dashdiagram" ]
            dir
            DocumentModuleKind.Tab
            state
            options
            (Some refs)
            None

        Assert.True(state.Diagrams.ContainsKey "used_heatmap")
        Assert.False(state.Diagrams.ContainsKey "broken")

        try
            Directory.Delete(dir, true)
        with _ ->
            ()

    [<Fact>]
    let ``LegacySequential membership registers every valid diagram in glob`` () =
        let dir = Path.Combine(Path.GetTempPath(), "dashspec-link-legacy-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore

        write dir "a.dashdiagram" "@diagram a\nheatmap { }\n"
        write dir "b.dashdiagram" "@diagram b\nheatmap { }\n"

        let state = ModuleIncludeState()

        let options =
            { DashSpecParseOptions.defaultOptions with
                LinkOnlyReferencedDiagramUnits = false }

        IncludeExpander.linkEnvelope
            [ ModuleLinkDirective.PathReference "*.dashdiagram" ]
            dir
            DocumentModuleKind.Tab
            state
            options
            None
            None

        Assert.Equal(2, state.Diagrams.Count)

        try
            Directory.Delete(dir, true)
        with _ ->
            ()
