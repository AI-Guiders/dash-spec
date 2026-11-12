namespace DashSpec.Modeling.Parse.Syntax

open System.Collections.Generic
open DashSpec.Modeling.Parse.Lexing

/// Physical line slices from a single token cursor (shared by surface syntax + format helpers).
/// Multi-line bracket lists on the right-hand side of `=` (`key = [\n … \n]`) stay in one statement.
module TokenReaderPhysicalLines =

    type PhysicalLine =
        { Span: TextSpan
          Tokens: Token list }

    let readNext (reader: TokenReader) : PhysicalLine option =
        if reader.IsEofKeepingTrivia then
            None
        else
            let tokens = reader.Tokens
            let index = reader.TokenIndex

            if tokens.[index].Kind = TokenKind.Newline then
                let start = tokens.[index].Start
                let length = max 1 tokens.[index].Length
                reader.Advance()
                Some { Span = TextSpan.Create start length; Tokens = [] }
            else
                let lineTokens = ResizeArray<Token>()
                let start = tokens.[index].Start
                let mutable idx = index
                let mutable seenEq = false
                let mutable bracketDepth = 0

                while idx < tokens.Count
                      && tokens.[idx].Kind <> TokenKind.Eof
                      && (tokens.[idx].Kind <> TokenKind.Newline || (seenEq && bracketDepth > 0)) do
                    let tok = tokens.[idx]
                    if tok.Kind = TokenKind.Eq then
                        seenEq <- true
                    elif seenEq && tok.Kind = TokenKind.LBracket then
                        bracketDepth <- bracketDepth + 1
                    elif seenEq && tok.Kind = TokenKind.RBracket && bracketDepth > 0 then
                        bracketDepth <- bracketDepth - 1

                    if tok.Kind <> TokenKind.Newline then
                        lineTokens.Add tok

                    idx <- idx + 1

                reader.SetTokenIndex idx

                if idx < tokens.Count && tokens.[idx].Kind = TokenKind.Newline then
                    reader.Advance()

                let list = lineTokens |> Seq.toList

                let length =
                    if list.IsEmpty then
                        0
                    else
                        let last = List.last list
                        last.Start + max 1 last.Length - start

                Some { Span = TextSpan.Create start length; Tokens = list }
