namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Document
open DashSpec.Modeling.Parse.Syntax

module DashSpecRuleChecksTests =

    let private codesOf (text: string) =
        DashSpecRuleChecks.findings (DashSpecSurfaceSyntax.parse text).Root
        |> List.map (fun finding -> DashSpecRules.code finding.Rule)

    [<Fact>]
    let ``balanced module has no rule findings`` () =
        Assert.Empty(codesOf "module host h\nend host\n")

    [<Fact>]
    let ``extra end block is DS001`` () =
        Assert.Contains("DS001", codesOf "module host h\nend host\nend host\n")

    [<Fact>]
    let ``end of another kind is DS002`` () =
        Assert.Contains("DS002", codesOf "module host h\nconfiguration\nend presentation\nend configuration\nend host\n")

    [<Fact>]
    let ``unclosed block is DS004`` () =
        Assert.Contains("DS004", codesOf "module host h\nconfiguration\n  language = \"ru-RU\"\n")

    [<Fact>]
    let ``import after code is DS008`` () =
        Assert.Contains("DS008", codesOf "module host h\nconfiguration\nend configuration\nimport layouts from Demo.Layouts\nend host\n")

    [<Fact>]
    let ``parse failure becomes DS009 diagnostic`` () =
        let diagnostics =
            DashSpecCompiler.diagnose "module dashboard d\nnot_a_keyword here\nend dashboard\n" None DashSpecParseOptions.defaultOptions

        Assert.True(diagnostics |> List.exists (fun d -> d.Code = Some "DS009"))
