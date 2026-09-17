namespace DashSpec.Modeling.Parse.Lexing

open System
open System.Collections.Generic
open System.Text
open DashSpec.Modeling.Core

/// Character scanner for .dashspec — keywords recognized by parser, not lexer.
module DashSpecLexer =

    let private isIdentStart (c: char) =
        Char.IsLetter c || c = '_' || c = '.' || Char.IsDigit c

    let private isIdentPart (c: char) = Char.IsLetterOrDigit c || c = '_' || c = '.'

    let private skipToEndOfLine (text: string) (i: byref<int>) =
        while i < text.Length && text.[i] <> '\r' && text.[i] <> '\n' do
            i <- i + 1

    let private tryReadLineComment (text: string) (i: byref<int>) atLineStart =
        if not atLineStart || i + 1 >= text.Length || text.[i + 1] <> '/' then None
        else
            let start = i
            i <- i + 2
            skipToEndOfLine text &i
            Some { Kind = TokenKind.LineComment; Value = text.[start..i - 1]; Start = start; Length = i - start }

    let private tryReadBlockComment (text: string) (i: byref<int>) (atLineStart: byref<bool>) =
        if i + 1 >= text.Length || text.[i + 1] <> '*' then None
        else
            let start = i
            i <- i + 2
            let mutable closed = false
            while i < text.Length && not closed do
                if text.[i] = '\r' || text.[i] = '\n' then
                    if text.[i] = '\r' then i <- i + 1
                    if i < text.Length && text.[i] = '\n' then i <- i + 1
                    atLineStart <- true
                elif text.[i] = '*' && i + 1 < text.Length && text.[i + 1] = '/' then
                    i <- i + 2
                    if i < text.Length && text.[i] <> '\r' && text.[i] <> '\n' then
                        atLineStart <- false
                    closed <- true
                else
                    i <- i + 1
            if closed then
                Some { Kind = TokenKind.BlockComment; Value = text.[start..i - 1]; Start = start; Length = i - start }
            else
                raise (DashSpecParseException("Unterminated block comment (/* … */)."))

    let private readString (text: string) (i: byref<int>) start =
        i <- i + 1
        let sb = StringBuilder()
        while i < text.Length && text.[i] <> '"' do
            if text.[i] = '\\' && i + 1 < text.Length then
                i <- i + 1
                sb.Append(if text.[i] = 'n' then '\n' else text.[i]) |> ignore
                i <- i + 1
            else
                sb.Append(text.[i]) |> ignore
                i <- i + 1
        if i >= text.Length then
            raise (DashSpecParseException("Unterminated string literal."))
        i <- i + 1
        { Kind = TokenKind.String; Value = sb.ToString(); Start = start; Length = i - start }

    let private readMultilineString (text: string) (i: byref<int>) start =
        i <- i + 3
        let sb = StringBuilder()
        let mutable finished = false
        let mutable token = Unchecked.defaultof<Token>

        while i < text.Length && not finished do
            if i + 2 < text.Length && text.[i] = '"' && text.[i + 1] = '"' && text.[i + 2] = '"' then
                i <- i + 3
                token <-
                    { Kind = TokenKind.String
                      Value = sb.ToString()
                      Start = start
                      Length = i - start }
                finished <- true
            elif text.[i] = '\r' then
                sb.Append('\n') |> ignore
                i <- i + 1
                if i < text.Length && text.[i] = '\n' then i <- i + 1
            else
                sb.Append(text.[i]) |> ignore
                i <- i + 1

        if finished then token
        else raise (DashSpecParseException("Unterminated multiline string literal (\"\"\")."))

    let private readHexColor (text: string) (i: byref<int>) start =
        i <- i + 1
        let hexStart = i
        while i < text.Length && Uri.IsHexDigit(text.[i]) do
            i <- i + 1
        let hexLen = i - hexStart
        if hexLen <> 3 && hexLen <> 6 then
            raise (DashSpecParseException($"Invalid hex color at position {start}. Use #rgb or #rrggbb (not at line start as comment — use // or /* */)."))
        { Kind = TokenKind.HexColor; Value = text.[start..i - 1]; Start = start; Length = i - start }

    let tokenize (text: string) : IReadOnlyList<Token> =
        let tokens = ResizeArray<Token>()
        let mutable i = 0
        let mutable atLineStart = true
        while i < text.Length do
            if Char.IsWhiteSpace text.[i] then
                if text.[i] = '\r' || text.[i] = '\n' then
                    tokens.Add({ Kind = TokenKind.Newline; Value = "\n"; Start = i; Length = 0 })
                    i <- i + 1
                    if i < text.Length && text.[i] = '\n' && text.[i - 1] = '\r' then i <- i + 1
                    atLineStart <- true
                else
                    i <- i + 1
            elif text.[i] = '/' then
                let mutable ii = i
                let mutable als = atLineStart
                match tryReadBlockComment text &ii &als with
                | Some comment ->
                    tokens.Add comment
                    i <- ii
                    atLineStart <- als
                | None ->
                    match tryReadLineComment text &ii atLineStart with
                    | Some comment ->
                        tokens.Add comment
                        i <- ii
                    | None ->
                        tokens.Add({ Kind = TokenKind.Slash; Value = "/"; Start = i; Length = 1 })
                        i <- i + 1
                        atLineStart <- false
            else
                let start = i
                match text.[i] with
                | '@' -> tokens.Add({ Kind = TokenKind.At; Value = "@"; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '!' -> tokens.Add({ Kind = TokenKind.Bang; Value = "!"; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '{' -> tokens.Add({ Kind = TokenKind.LBrace; Value = "{"; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '}' -> tokens.Add({ Kind = TokenKind.RBrace; Value = "}"; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '=' -> tokens.Add({ Kind = TokenKind.Eq; Value = "="; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '.' ->
                    if i + 1 < text.Length && text.[i + 1] = '.' then
                        tokens.Add({ Kind = TokenKind.DotDot; Value = ".."; Start = start; Length = 2 }); i <- i + 2; atLineStart <- false
                    else
                        tokens.Add({ Kind = TokenKind.Dot; Value = "."; Start = start; Length = 1 })
                        i <- i + 1
                        atLineStart <- false
                | '-' ->
                    let relStart = i
                    i <- i + 1
                    while i < text.Length && (Char.IsDigit text.[i] || text.[i] = 'd' || text.[i] = 'D') do
                        i <- i + 1
                    if i <= relStart + 1 then
                        raise (DashSpecParseException($"Invalid relative day at position {relStart}. Use form -Nd, e.g. -7d."))
                    tokens.Add({ Kind = TokenKind.RelativeDay; Value = text.[relStart..i - 1]; Start = relStart; Length = i - relStart })
                    atLineStart <- false
                | ',' -> tokens.Add({ Kind = TokenKind.Comma; Value = ","; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '"' ->
                    let tok =
                        if i + 2 < text.Length && text.[i + 1] = '"' && text.[i + 2] = '"' then readMultilineString text &i start
                        else readString text &i start
                    tokens.Add tok; atLineStart <- false
                | '#' -> tokens.Add(readHexColor text &i start); atLineStart <- false
                | '[' ->
                    if i + 1 < text.Length && text.[i + 1] = '[' then
                        let mutable j = i + 2
                        let mutable foundEnd = false
                        while j < text.Length && not foundEnd do
                            if text.[j] = ']' && j + 1 < text.Length && text.[j + 1] = ']' then
                                j <- j + 2
                                foundEnd <- true
                            else
                                j <- j + 1
                        if j >= text.Length then raise (DashSpecParseException("Unterminated [[ raw block."))
                        tokens.Add({ Kind = TokenKind.Raw; Value = text.[start..j - 1].Trim(); Start = start; Length = j - start })
                        i <- j; atLineStart <- false
                    else
                        tokens.Add({ Kind = TokenKind.LBracket; Value = "["; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | ']' -> tokens.Add({ Kind = TokenKind.RBracket; Value = "]"; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | '(' -> tokens.Add({ Kind = TokenKind.LParen; Value = "("; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | ')' -> tokens.Add({ Kind = TokenKind.RParen; Value = ")"; Start = start; Length = 1 }); i <- i + 1; atLineStart <- false
                | c when isIdentStart c ->
                    i <- i + 1
                    while i < text.Length && isIdentPart text.[i] do i <- i + 1
                    tokens.Add({ Kind = TokenKind.Ident; Value = text.[start..i - 1]; Start = start; Length = i - start })
                    atLineStart <- false
                | c -> raise (DashSpecParseException($"Unexpected character '{c}' at position {i}.", i))
        tokens.Add({ Kind = TokenKind.Eof; Value = ""; Start = text.Length; Length = 0 })
        tokens :> IReadOnlyList<Token>

