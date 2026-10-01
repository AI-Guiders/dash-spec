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

/// <summary>Inline tooltip body slice for C# <c>TooltipModuleParser</c> bridge.</summary>
module TokenReaderTooltip =

    let private skipVariablesBlock (reader: TokenReader) (bodyEnd: byref<int>) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "variables" None) && not reader.IsEof do
            reader.SkipNewlines()
            if not (BlockSyntax.isBlockEnd reader "variables" None) then
                reader.ReadIdent() |> ignore
                reader.Expect TokenKind.Eq
                reader.ReadIdent() |> ignore
                bodyEnd <- reader.EndOffset(reader.TokenIndex - 1)
                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "variables" None
        if reader.TokenIndex > 0 then
            bodyEnd <- reader.EndOffset(reader.TokenIndex - 1)

    let readBodySource (reader: TokenReader) =
        match reader.SourceText with
        | None ->
            raise (System.InvalidOperationException("Tooltip inline parse requires source text on TokenReader."))
        | Some text ->
            reader.SkipNewlines()
            let start = reader.Tokens.[reader.TokenIndex].Start
            let mutable bodyEnd = start

            let mutable continueLoop = true
            while continueLoop && not reader.IsEof do
                reader.SkipNewlines()
                if reader.IsEof then
                    continueLoop <- false
                elif reader.TryKeyword "variables" then
                    skipVariablesBlock reader &bodyEnd
                elif reader.TryKeyword "tooltip" then
                    reader.Expect TokenKind.Eq
                    reader.ReadString() |> ignore
                    bodyEnd <- reader.EndOffset(reader.TokenIndex - 1)
                elif reader.TryKeyword "source" then
                    reader.Expect TokenKind.Eq
                    reader.ReadIdent() |> ignore
                    bodyEnd <- reader.EndOffset(reader.TokenIndex - 1)
                elif reader.TryKeyword "end" && reader.TryKeyword "tooltip" then
                    continueLoop <- false
                else
                    raise (reader.Unexpected())

            text.Substring(start, bodyEnd - start)