namespace DashSpec.Modeling.Parse.Syntax

open System.Collections.Generic
open DashSpec.Modeling.Parse.Lexing

module SyntaxTree =

    /// Outline projection only — prefer <c>DashSpec.Modeling.Authoring.DashSpecAuthoringEntry.parse</c> for IR + graph.
    let parse text = DashSpecSurfaceSyntax.parse text

    let classifiedSpans (tree: ParseTree) : IReadOnlyList<DashSpecSyntaxSpan> =
        tree.Tokens
        |> Array.choose (fun token ->
            match token.LexKind with
            | TokenKind.Newline | TokenKind.Eof -> None
            | _ ->
                Some
                    { Start = token.Span.Start
                      Length = token.Span.Length
                      Kind = token.Kind })
        |> fun spans -> spans :> IReadOnlyList<_>

    let findNodeAt (tree: ParseTree) (offset: int) : DashSpecAstNode option =
        let rec walk (node: DashSpecAstNode) =
            if not ((DashSpecAst.span node).Contains offset) then
                None
            else
                let mutable deepest = node

                for child in DashSpecAst.members node do
                    match walk child with
                    | Some deeper when (DashSpecAst.span deeper).Contains offset -> deepest <- deeper
                    | _ -> ()

                Some deepest

        walk tree.Root
