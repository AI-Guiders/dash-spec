namespace DashSpec.Modeling.Parse.Phrase

open System.Collections.Generic
open DashSpec.Modeling.Parse.Lexing

type PhraseTokenKind =
    | Ident = 0
    | String = 1
    | Int = 2

type PhraseToken = { Kind: PhraseTokenKind; Value: string }

module PhraseLineReader =

    let readLineTokens (reader: TokenReader) =
        reader.SkipNewlines()
        let tokens = ResizeArray<PhraseToken>()

        while not (reader.IsAt TokenKind.Newline)
              && not (reader.IsAt TokenKind.RBrace)
              && not reader.IsEof do
            match reader.CurrentKind with
            | TokenKind.Ident -> tokens.Add({ Kind = PhraseTokenKind.Ident; Value = reader.ReadIdent() })
            | TokenKind.String -> tokens.Add({ Kind = PhraseTokenKind.String; Value = reader.ReadString() })
            | TokenKind.LParen
            | TokenKind.RParen
            | TokenKind.Comma -> reader.Advance()
            | _ -> ()
            if tokens.Count = 0 && reader.IsAt TokenKind.Newline then ()

        tokens :> IReadOnlyList<_>
