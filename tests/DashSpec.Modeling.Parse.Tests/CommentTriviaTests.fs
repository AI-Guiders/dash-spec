namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

module CommentTriviaTests =

    let private moduleMembers (text: string) =
        let root = (DashSpecSurfaceSyntax.parse text).Root

        let moduleNode =
            DashSpecAst.members root
            |> Array.find (fun node ->
                match node with
                | DashSpecAstNode.ModuleDeclaration _ -> true
                | _ -> false)

        DashSpecAst.members moduleNode

    [<Fact>]
    let ``comment-only line inside a module is kept as a trivia line`` () =
        let members = moduleMembers "module dashboard d\n  // note\n  title = \"x\"\nend dashboard\n"

        Assert.Equal(3, members.Length)
        Assert.True(DashSpecAst.isTrivia members.[0])
        Assert.False(DashSpecAst.isTrivia members.[1])

    [<Fact>]
    let ``comment before import in module header is not code`` () =
        let codes =
            DashSpecRuleChecks.findings (DashSpecSurfaceSyntax.parse "module host h\n  // note\n  import layouts from X\nend host\n").Root
            |> List.map (fun finding -> DashSpecRules.code finding.Rule)

        Assert.DoesNotContain("DS008", codes)

    [<Fact>]
    let ``comment-only lines produce no rule findings`` () =
        let findings =
            DashSpecRuleChecks.findings (DashSpecSurfaceSyntax.parse "// top\nmodule host h\n  // note\nend host\n").Root

        Assert.Empty(findings)