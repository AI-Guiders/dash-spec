namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Syntax

module SyntaxClassifierTests =

    [<Fact>]
    let ``tab dashspec module keeps nested tab and cards as keywords`` () =
        let text =
            "        tab analytics as \"Analytics\" dashspec \"demo-analytics.dashspec\"\n            tab detail as \"Detail\"\n                cards\n                    events_detail\n"

        let kinds = DashSpecSyntaxClassifier.classify text |> Seq.map (fun s -> s.Kind) |> Seq.toList
        let keywordCount = kinds |> List.filter ((=) DashSpecSyntaxKind.Keyword) |> List.length
        Assert.True(keywordCount >= 6)
        Assert.True(List.contains DashSpecSyntaxKind.String kinds)

    [<Fact>]
    let ``comments are classified`` () =
        let text = "// comment\n@dashboard demo\nend dashboard\n"
        let kinds = DashSpecSyntaxClassifier.classify text |> Seq.map (fun s -> s.Kind) |> Seq.toList
        Assert.True(List.contains DashSpecSyntaxKind.Comment kinds)
        Assert.True(List.contains DashSpecSyntaxKind.ModuleHeader kinds)
        Assert.True(List.contains DashSpecSyntaxKind.EndKeyword kinds)
