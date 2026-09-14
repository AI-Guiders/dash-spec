namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Phrase

module CardClickParser =

    let private validateKnownHandler (cardId: string) (handlerId: string) (parseOptions: DashSpecParseOptions) =
        if parseOptions.KnownActionHandlers.Count = 0 && parseOptions.KnownInteractionHandlers.Count = 0 then ()
        elif parseOptions.KnownActionHandlers.Contains handlerId
             || parseOptions.KnownInteractionHandlers.Contains handlerId then
            ()
        else
            raise (DashSpecParseException($"Card '{cardId}': unknown handler '{handlerId}'."))

    let private parseCallArgs (reader: TokenReader) (cardId: string) =
        let args = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        if not (reader.IsAt TokenKind.LParen) then args :> IReadOnlyDictionary<_, _>
        else
            reader.Expect TokenKind.LParen
            reader.SkipNewlines()
            while not (reader.IsAt TokenKind.RParen) && not reader.IsEof do
                reader.SkipNewlines()
                if reader.IsAt TokenKind.RParen then ()
                else
                    let name = reader.ReadIdent()
                    if String.IsNullOrWhiteSpace name then
                        raise (DashSpecParseException($"Card '{cardId}': expected argument name in invoke/run call."))
                    reader.Expect TokenKind.Eq
                    args.[name] <- reader.ReadScalarValue()
                    reader.SkipNewlines()
                    if reader.IsAt TokenKind.Comma then reader.Advance()
            reader.Expect TokenKind.RParen
            args :> IReadOnlyDictionary<_, _>

    let private parseInvokeEffect (reader: TokenReader) (cardId: string) (parseOptions: DashSpecParseOptions) =
        let handlerId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace handlerId then
            raise (DashSpecParseException($"Card '{cardId}': invoke requires handler id."))
        validateKnownHandler cardId handlerId parseOptions
        InvokeHandler(handlerId, parseCallArgs reader cardId)

    let private parseShowEffect (reader: TokenReader) (cardId: string) =
        if not (reader.TryKeyword "below") then
            raise (DashSpecParseException($"Card '{cardId}': show supports only 'below' placement in v1."))
        if not (reader.TryKeyword "as") then
            raise (DashSpecParseException($"Card '{cardId}': show below requires 'as list|plain|kv'."))
        let formatToken = reader.ReadIdent()
        if String.IsNullOrWhiteSpace formatToken then
            raise (DashSpecParseException($"Card '{cardId}': show below requires 'as list|plain|kv'."))
        let format =
            match formatToken.ToLowerInvariant() with
            | "list" -> ShowFormat.List
            | "plain" -> ShowFormat.Plain
            | "kv" -> ShowFormat.Kv
            | _ ->
                raise (DashSpecParseException($"Card '{cardId}': show format must be list, plain, or kv; got '{formatToken}'."))
        if not (reader.TryKeyword "from") then
            raise (DashSpecParseException($"Card '{cardId}': show below requires 'from tooltip|cell'."))
        let sourceToken = reader.ReadIdent()
        if String.IsNullOrWhiteSpace sourceToken then
            raise (DashSpecParseException($"Card '{cardId}': show below requires 'from tooltip|cell'."))
        let source =
            match sourceToken.ToLowerInvariant() with
            | "tooltip" -> ShowSource.Tooltip
            | "cell" -> ShowSource.Cell
            | _ ->
                raise (DashSpecParseException($"Card '{cardId}': show source must be tooltip or cell; got '{sourceToken}'."))
        let copyFriendly = reader.TryKeyword "copy"
        let split =
            if reader.TryKeyword "split" then
                if reader.IsAt TokenKind.Eq then reader.Expect TokenKind.Eq
                Some(reader.ReadString())
            else
                None
        ShowSelection(ShowPlacement.Below, format, source, copyFriendly, split)

    let private parseSetEffect (reader: TokenReader) (cardId: string) =
        let filterName = reader.ReadIdent()
        if String.IsNullOrWhiteSpace filterName then
            raise (DashSpecParseException($"Card '{cardId}': set requires filter name."))
        if not (reader.TryKeyword "from") then
            raise (DashSpecParseException($"Card '{cardId}': set {filterName} requires 'from x|y|value'."))
        let field = reader.ReadIdent()
        if String.IsNullOrWhiteSpace field then
            raise (DashSpecParseException($"Card '{cardId}': set {filterName} requires 'from x|y|value'."))
        if not (field.Equals("x", StringComparison.OrdinalIgnoreCase)
                || field.Equals("y", StringComparison.OrdinalIgnoreCase)
                || field.Equals("value", StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Card '{cardId}': set from field must be x, y, or value; got '{field}'."))
        SetFilterFromField(filterName, field.ToLowerInvariant())

    let private parsePreserveFilterList (reader: TokenReader) =
        reader.SkipNewlines()
        if reader.IsEof then Array.empty :> IReadOnlyList<_>
        elif
            match reader.TryPeekIdent() with
            | Some next when String.Equals(next, "end", StringComparison.OrdinalIgnoreCase) -> true
            | _ -> false
        then
            Array.empty :> IReadOnlyList<_>
        else
            reader.ReadCommaListInline()

    let private parseGotoEffect (reader: TokenReader) (cardId: string) =
        if reader.TryKeyword "tab" then
            let tabId = reader.ReadIdent()
            if String.IsNullOrWhiteSpace tabId then
                raise (DashSpecParseException($"Card '{cardId}': goto tab requires tab id."))
            GotoTab tabId
        elif reader.TryKeyword "page" then
            let pageId = reader.ReadIdent()
            if String.IsNullOrWhiteSpace pageId then
                raise (DashSpecParseException($"Card '{cardId}': goto page requires page id."))
            GotoPage pageId
        elif reader.TryKeyword "entry" then
            let entryId = reader.ReadIdent()
            if String.IsNullOrWhiteSpace entryId then
                raise (DashSpecParseException($"Card '{cardId}': goto entry requires catalog entry id."))
            let preserve =
                if reader.TryKeyword "preserving" then
                    if not (reader.TryKeyword "filters") then
                        raise (DashSpecParseException($"Card '{cardId}': goto entry preserving requires 'filters' keyword."))
                    Some(parsePreserveFilterList reader)
                else
                    None
            GotoCatalogEntry(entryId, preserve)
        else
            raise (DashSpecParseException($"Card '{cardId}': goto requires tab, page, or entry."))

    let private parseFocusEffect (reader: TokenReader) (cardId: string) =
        let phaseId = reader.ReadIdent()
        if String.IsNullOrWhiteSpace phaseId then
            raise (DashSpecParseException($"Card '{cardId}': focus requires phase id."))
        FocusPhase phaseId

    let private tryParsePhraseEffect
        (reader: TokenReader)
        (cardId: string)
        (templates: IReadOnlyList<PhraseTemplateDescriptor>)
        =
        if templates.Count = 0 then None
        else
            let tokens = PhraseLineReader.readLineTokens reader
            if tokens.Count = 0 then None
            else
                match PhraseTemplateMatcher.tryMatchAny tokens templates with
                | Some(handlerId, args) -> Some(InvokeHandler(handlerId, args))
                | None ->
                    raise (DashSpecParseException($"Card '{cardId}': unrecognized phrase in on click block."))

    let parseClickBlock (reader: TokenReader) (cardId: string) (parseOptions: DashSpecParseOptions) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let effects = ResizeArray<CardClickEffect>()
        let phraseTemplates =
            parseOptions.PhraseTemplates
            |> Seq.filter (fun x -> String.Equals(x.Scope, PhraseScopes.onClick, StringComparison.OrdinalIgnoreCase))
            |> Seq.toArray

        while not (BlockSyntax.isBlockEnd reader "click" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "click" None then ()
            elif reader.TryKeyword "show" then
                effects.Add(parseShowEffect reader cardId)
                reader.SkipNewlines()
            elif reader.TryKeyword "set" then
                effects.Add(parseSetEffect reader cardId)
                reader.SkipNewlines()
            elif reader.TryKeyword "goto" then
                effects.Add(parseGotoEffect reader cardId)
                reader.SkipNewlines()
            elif reader.TryKeyword "focus" then
                effects.Add(parseFocusEffect reader cardId)
                reader.SkipNewlines()
            elif reader.TryKeyword "invoke" || reader.TryKeyword "run" then
                effects.Add(parseInvokeEffect reader cardId parseOptions)
                reader.SkipNewlines()
            else
                match tryParsePhraseEffect reader cardId phraseTemplates with
                | Some effect ->
                    effects.Add(effect)
                    reader.SkipNewlines()
                | None -> raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "click" None
        if effects.Count = 0 then
            raise (DashSpecParseException($"Card '{cardId}': on click block requires at least one effect."))
        { Effects = effects :> IReadOnlyList<_> }
