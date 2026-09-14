namespace DashSpec.Modeling.Parse

open System
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Core

/// Dual block syntax: legacy { … } or … end kind (ADR-0036).
module BlockSyntax =

    let beginBlock (reader: TokenReader) =
        reader.SkipNewlines()
        if reader.IsAt TokenKind.LBrace then
            reader.Advance()
            reader.PushBlockClose BlockCloseStyle.Brace
        else
            reader.PushBlockClose BlockCloseStyle.EndKeyword

    let private readEndKind (reader: TokenReader) =
        if reader.TryKeyword "on" then
            reader.ExpectKeyword "click"
            "click"
        else
            reader.ReadIdent()

    let tryMatchEnd (reader: TokenReader) (expectedKind: string) (expectedId: string option) (consume: bool) =
        reader.SkipNewlines()
        let saved = reader.SavePosition()
        if not (reader.TryKeyword "end") then false
        else
            let actualKind = readEndKind reader
            if not (String.Equals(actualKind, expectedKind, StringComparison.OrdinalIgnoreCase)) then
                reader.RestorePosition saved
                false
            else
                let actualId =
                    if reader.RawKind = TokenKind.Ident && not (reader.IsOnNewline()) then
                        Some(reader.ReadIdentSameLine())
                    else None
                match expectedId, actualId with
                | Some eid, Some aid when not (String.Equals(aid, eid, StringComparison.OrdinalIgnoreCase)) ->
                    reader.RestorePosition saved
                    false
                | _ ->
                    if not consume then reader.RestorePosition saved
                    true

    let isBlockEnd (reader: TokenReader) (endKind: string) (endId: string option) =
        match reader.PeekBlockClose() with
        | BlockCloseStyle.Brace -> reader.IsAt TokenKind.RBrace
        | _ -> tryMatchEnd reader endKind endId false

    let expectBlockEnd (reader: TokenReader) (endKind: string) (endId: string option) =
        match reader.PeekBlockClose() with
        | BlockCloseStyle.Brace ->
            reader.Expect TokenKind.RBrace
            reader.PopBlockClose()
        | _ when tryMatchEnd reader endKind endId true -> reader.PopBlockClose()
        | _ ->
            let label = match endId with Some id -> $"{endKind} {id}" | None -> endKind
            raise (DashSpec.Modeling.Core.DashSpecParseException($"Expected end {label}."))