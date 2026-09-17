namespace DashSpec.Modeling.Parse.Syntax

open System.Collections.Generic

module SyntaxTree =

    let parse text = SyntaxTreeParser.parse text

    let classifiedSpans (tree: ParseTree) : IReadOnlyList<DashSpecSyntaxSpan> =
        let spans = ResizeArray<DashSpecSyntaxSpan>()

        let rec walk (node: SyntaxNode) =
            for token in node.Tokens do
                spans.Add(
                    { Start = token.Span.Start
                      Length = token.Span.Length
                      Kind = token.Kind }
                )
            for child in node.Children do
                walk child

        walk tree.Root
        spans :> IReadOnlyList<_>

    let findNodeAt (tree: ParseTree) (offset: int) : SyntaxNode option =
        let rec walk (node: SyntaxNode) =
            if not (node.Span.Contains offset) then None
            else
                let mutable deepest = node
                for child in node.Children do
                    match walk child with
                    | Some deeper when deeper.Span.Contains offset -> deepest <- deeper
                    | _ -> ()
                Some deepest

        walk tree.Root
