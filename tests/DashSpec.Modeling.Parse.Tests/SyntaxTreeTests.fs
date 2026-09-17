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
            tree.Root.Children
            |> Array.filter (fun node -> node.Kind = SyntaxNodeKind.Block)

        Assert.True(blocks.Length >= 1)
        let outerTab = blocks.[0]
        Assert.True(outerTab.Children.Length >= 1)
        let innerTab = outerTab.Children |> Array.find (fun node -> node.Kind = SyntaxNodeKind.Block)
        Assert.True(innerTab.Children |> Array.exists (fun node -> node.Kind = SyntaxNodeKind.Block))

    [<Fact>]
    let ``findNodeAt resolves nested tab line`` () =
        let tree = SyntaxTree.parse nestedTabText
        let offset = tree.Text.IndexOf("tab detail")
        Assert.True(offset >= 0)

        match SyntaxTree.findNodeAt tree offset with
        | Some node ->
            Assert.Equal(SyntaxNodeKind.Block, node.Kind)
            Assert.True(node.Tokens |> Array.exists (fun token -> token.Text = "tab"))
        | None -> Assert.Fail("expected block node at nested tab offset")

    [<Fact>]
    let ``classified spans match classifier projection`` () =
        let text = "// comment\n@dashboard demo\nend dashboard\n"
        let fromTree = text |> SyntaxTree.parse |> SyntaxTree.classifiedSpans
        let fromClassifier = DashSpecSyntaxClassifier.classify text
        Assert.Equal<string>(fromClassifier |> Seq.map (fun s -> $"{s.Start}:{s.Length}:{s.Kind}"), fromTree |> Seq.map (fun s -> $"{s.Start}:{s.Length}:{s.Kind}"))
