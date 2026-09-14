namespace DashSpec.Modeling.Parse.Card

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

module CardVisibilityParser =

    let private isWhenBlockEnd (reader: TokenReader) (filterName: string option) =
        BlockSyntax.isBlockEnd reader "when" None
        || BlockSyntax.isBlockEnd reader "oversize" None
        || (filterName.IsSome && BlockSyntax.isBlockEnd reader filterName.Value None)

    let private expectWhenBlockEnd (reader: TokenReader) (filterName: string option) =
        if filterName.IsSome && BlockSyntax.isBlockEnd reader filterName.Value None then
            BlockSyntax.expectBlockEnd reader filterName.Value None
        elif BlockSyntax.isBlockEnd reader "oversize" None then
            BlockSyntax.expectBlockEnd reader "oversize" None
        else
            BlockSyntax.expectBlockEnd reader "when" None

    let private readMessageBody (reader: TokenReader) (cardId: string) (filterName: string) =
        reader.SkipNewlines()
        let mutable message: string option = None

        while not (isWhenBlockEnd reader (Some filterName)) && not reader.IsEof do
            reader.SkipNewlines()
            if isWhenBlockEnd reader (Some filterName) then ()
            else
                let key = reader.ReadIdent()
                if not (String.Equals(key, "message", StringComparison.OrdinalIgnoreCase)) then
                    raise (DashSpecParseException($"Card '{cardId}': when block supports only message = \"…\"."))
                reader.Expect TokenKind.Eq
                message <- Some(reader.ReadString())
                reader.SkipNewlines()

        expectWhenBlockEnd reader (Some filterName)

        match message with
        | None | Some (null | "") ->
            raise (DashSpecParseException($"Card '{cardId}': when message cannot be empty."))
        | Some value -> value

    let private tryReadMessageBody (reader: TokenReader) (cardId: string) (filterName: string) =
        if reader.IsEof || isWhenBlockEnd reader (Some filterName) then None
        else Some(readMessageBody reader cardId filterName)

    let parseFilterWhen (reader: TokenReader) (cardId: string) (filterName: string) =
        if String.IsNullOrWhiteSpace filterName then
            raise (DashSpecParseException($"Card '{cardId}': when requires filter name or oversize."))

        if reader.TryKeyword "empty" then
            { FilterName = filterName; Mode = CardVisibilityMode.WhenEmpty; Message = None }
        elif reader.TryKeyword "set" then
            reader.SkipNewlines()
            { FilterName = filterName
              Mode = CardVisibilityMode.WhenSet
              Message = tryReadMessageBody reader cardId filterName }
        else
            reader.SkipNewlines()
            if not reader.IsEof && reader.TryKeyword "message" then
                reader.Expect TokenKind.Eq
                let inlineMessage = reader.ReadString()
                reader.SkipNewlines()
                expectWhenBlockEnd reader (Some filterName)
                { FilterName = filterName; Mode = CardVisibilityMode.WhenSet; Message = Some inlineMessage }
            elif not reader.IsEof && not (isWhenBlockEnd reader (Some filterName)) then
                { FilterName = filterName
                  Mode = CardVisibilityMode.WhenSet
                  Message = Some(readMessageBody reader cardId filterName) }
            else
                { FilterName = filterName; Mode = CardVisibilityMode.WhenSet; Message = None }

    let parseOversizeWhen (reader: TokenReader) (cardId: string) =
        reader.SkipNewlines()
        readMessageBody reader cardId "oversize"
