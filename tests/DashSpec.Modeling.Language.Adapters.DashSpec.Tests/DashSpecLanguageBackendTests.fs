namespace DashSpec.Modeling.Language.Adapters.DashSpec.Tests

open System.Threading
open Xunit
open AIGuiders.Platform.Modeling.Language
open DashSpec.Modeling.Language.Adapters.DashSpec

module DashSpecLanguageBackendTests =
    let private samplePath = "/tmp/demo-analytics.dashspec"

    let private sampleText =
        """@tab analytics
  filter period_grain on toolbar
  report
    card period_peak_by_app as "Peak by app"
      filters
        period_grain
      end filters
      diagram demo_period_peak_by_app_bar
    end card
  end report
"""

    let private request line column =
        { FilePath = samplePath
          Line = line
          Column = column
          SourceText = sampleText
          SolutionOrProjectPath = "" }

    let private backend () = DashSpecLanguageBackend() :> ILanguageBackend

    [<Fact>]
    let ``Get symbol at position resolves card declaration`` () =
        let symbol =
            backend().GetSymbolAtPositionAsync(request 4 10, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal("period_peak_by_app", symbol.Name)
        Assert.Equal("card", symbol.Kind)

    [<Fact>]
    let ``Find usages returns filter declaration and card reference`` () =
        let usages =
            backend().FindUsagesAsync(request 6 9, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.True(usages.References.Length >= 2)
        Assert.Contains(usages.References, fun ref -> ref.Span.Line = 2)
        Assert.Contains(usages.References, fun ref -> ref.Span.Line = 6)

    [<Fact>]
    let ``Go to definition resolves filter from card filters block`` () =
        let nav =
            backend().GoToDefinitionAsync(request 6 9, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Assert.Equal(samplePath, nav.Definition.Path)
        Assert.Equal(2, nav.Definition.Line)

    [<Fact>]
    let ``Rename symbol previews card rename without apply`` () =
        let renameReq =
            { Request = request 4 10
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
