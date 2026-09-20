namespace DashSpec.Modeling.Parse.Tests

open Xunit
open DashSpec.Modeling.Parse.Syntax

module SyntaxTreeTests =

    let private nestedTabText =
        "        tab analytics as \"Analytics\" dashspec \"demo-analytics.dashspec\"\n            tab detail as \"Detail\"\n                cards\n                    events_detail\n"

    [<Fact>]
    let ``parse builds nested block nodes`` () =
        let tree = SyntaxTree.parse nestedTabText
        let blocks =
            DashSpecAst.descendants tree.Root
            |> Seq.filter (function DashSpecAstNode.BlockDeclaration _ -> true | _ -> false)
            |> Seq.toArray

        Assert.True(blocks.Length >= 2)
        Assert.Contains(blocks, fun node ->
            match node with
            | DashSpecAstNode.BlockDeclaration { Opener = DashSpecBlockOpener.Named(DashSpecBlockKeyword.Tab, "detail") } -> true
            | _ -> false)

    [<Fact>]
    let ``parse builds card reference inside cards block`` () =
        let tree = SyntaxTree.parse nestedTabText

        Assert.Contains(
            DashSpecAst.descendants tree.Root,
            fun node ->
                match node with
                | DashSpecAstNode.CardReference { CardId = "events_detail" } -> true
                | _ -> false)

    [<Fact>]
    let ``findNodeAt resolves nested tab line`` () =
        let tree = SyntaxTree.parse nestedTabText
        let offset = tree.Text.IndexOf("tab detail")
        Assert.True(offset >= 0)

        match SyntaxTree.findNodeAt tree offset with
        | Some(DashSpecAstNode.BlockDeclaration block) ->
            Assert.Equal(DashSpecBlockKeyword.Tab, match block.Opener with | DashSpecBlockOpener.Named(k, _) -> k | _ -> failwith "expected named tab")
            Assert.True(block.Tokens |> Array.exists (fun token -> token.Text = "tab"))
        | _ -> Assert.Fail("expected block node at nested tab offset")

    [<Fact>]
    let ``classified spans match classifier projection`` () =
        let text = "// comment\n@dashboard demo\nend dashboard\n"
        let fromTree = text |> SyntaxTree.parse |> SyntaxTree.classifiedSpans
        let fromClassifier = DashSpecSyntaxClassifier.classify text
        Assert.Equal<string>(fromClassifier |> Seq.map (fun s -> $"{s.Start}:{s.Length}:{s.Kind}"), fromTree |> Seq.map (fun s -> $"{s.Start}:{s.Length}:{s.Kind}"))
