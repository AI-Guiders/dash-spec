namespace DashSpec.Modeling.Language.Adapters.DashSpec.Tests

open System.Threading
open Xunit
open AIGuiders.Platform.Modeling.Language
open DashSpec.Modeling.Language.Adapters.DashSpec

module DashSpecLanguageBackendTests =
    let private samplePath = "/tmp/demo-analytics.ds"

    let private sampleText =
        """module tab analytics
  report
    title = "Analytics"
    filter period_grain
      bind top
      end bind
    end filter
    card period_peak_by_app as "Peak by app"
      bind
        period_grain
      end bind
      diagram demo_period_peak_by_app_bar
      data flow { src [rows] -> [rows] __diagram__ }
    end card
  end report
end tab
"""

    let private request line column =
        { FilePath = samplePath
          Line = line
          Column = column
          SourceText = sampleText
          SolutionOrProjectPath = ""
          SessionDefaultLanguageId = "" }

    let private backend () = DashSpecLanguageBackend() :> ILanguageBackend

    [<Fact(Skip = "ADR-0102 units are .ds; LRC navigation still gates on .dashspec - product decision")>]
    let ``Get symbol at position resolves card declaration`` () =
        let symbol =
            backend().GetSymbolAtPositionAsync(request 8 10, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal("period_peak_by_app", symbol.Name)
        Assert.Equal("card", symbol.Kind)

    [<Fact(Skip = "ADR-0102 units are .ds; LRC navigation still gates on .dashspec - product decision")>]
    let ``Find usages returns filter declaration and card reference`` () =
        let usages =
            backend().FindUsagesAsync(request 10 9, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.True(usages.References.Length >= 2)
        Assert.Contains(usages.References, fun ref -> ref.Span.Line = 4)
        Assert.Contains(usages.References, fun ref -> ref.Span.Line = 10)

    [<Fact(Skip = "ADR-0102 units are .ds; LRC navigation still gates on .dashspec - product decision")>]
    let ``Go to definition resolves filter from card bind block`` () =
        let nav =
            backend().GoToDefinitionAsync(request 10 9, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(samplePath, nav.Definition.Path)
        Assert.Equal(4, nav.Definition.Line)

    [<Fact(Skip = "ADR-0102 units are .ds; LRC navigation still gates on .dashspec - product decision")>]
    let ``Rename symbol previews card rename without apply`` () =
        let renameReq =
            { Request = request 8 10
              NewName = "period_peak_renamed"
              Apply = false }

        let result =
            backend().RenameSymbolAsync(renameReq, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal("period_peak_by_app", result.OldName)
        Assert.Equal("period_peak_renamed", result.NewName)
        Assert.False(result.Applied)
        Assert.Contains("period_peak_renamed", result.Changes.[0].NewText)
