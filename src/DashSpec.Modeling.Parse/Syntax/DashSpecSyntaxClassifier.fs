namespace DashSpec.Modeling.Parse.Syntax

open System.Collections.Generic

module DashSpecSyntaxClassifier =

    let classify (text: string) : IReadOnlyList<DashSpecSyntaxSpan> =
        text |> SyntaxTree.parse |> SyntaxTree.classifiedSpans
