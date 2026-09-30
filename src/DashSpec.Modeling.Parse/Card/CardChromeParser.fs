namespace DashSpec.Modeling.Parse.Card

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing

module CardChromeParser =

    let private parseFoldMode (cardId: string) (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "none" -> CardFoldMode.None
        | "independent" | "titlebar" -> CardFoldMode.Independent
        | _ ->
            raise (DashSpecParseException($"Card '{cardId}': chrome fold must be none or independent; got '{raw}'."))

    let parse (reader: TokenReader) (cardId: string) =
        let props =
            BlockGrammar.parseKeywordContainer
                reader
                "chrome"
                $"card '{cardId}' chrome"
                [ BlockGrammar.SchemaProperties(PropertySchemas.cardChrome, false, false) ]
                None

        let boundFilters =
            match props.TryGetValue "bound_filters" with
            | true, raw ->
                match raw.Trim().ToLowerInvariant() with
                | "hidden" -> CardBoundFilterChrome.Hidden
                | "toolbar_only" | "toolbar-only" | "toolbaronly" -> CardBoundFilterChrome.ToolbarOnly
                | "chips" -> CardBoundFilterChrome.Chips
                | _ ->
                    raise (DashSpecParseException($"Card '{cardId}': chrome bound_filters must be chips, hidden, or toolbar_only; got '{raw}'."))
            | false, _ -> CardBoundFilterChrome.Chips

        let hideTitle =
            match props.TryGetValue "title" with
            | true, raw ->
                match raw.Trim().ToLowerInvariant() with
                | "hidden" | "omit" | "suppress" -> true
                | _ ->
                    raise (DashSpecParseException($"Card '{cardId}': chrome title must be hidden, omit, or suppress; got '{raw}'."))
            | false, _ -> false

        let fold =
            match props.TryGetValue "fold" with
            | true, raw -> parseFoldMode cardId raw
            | false, _ -> CardFoldMode.None

        { BoundFilters = boundFilters; HideTitle = hideTitle; Fold = fold }

module CardsChromeParser =

    let parse (reader: TokenReader) =
        let props =
            BlockGrammar.parseKeywordContainer
                reader
                "chrome"
                "cards chrome"
                [ BlockGrammar.SchemaProperties(PropertySchemas.cardsChrome, false, false) ]
                None

        let foldPolicy =
            match props.TryGetValue "fold" with
            | true, raw ->
                match raw.Trim().ToLowerInvariant() with
                | "none" -> CardsFoldPolicy.None
                | "focus_single" | "focus-single" | "focus" -> CardsFoldPolicy.FocusSingle
                | _ ->
                    raise (DashSpecParseException($"cards chrome fold must be none or focus_single; got '{raw}'."))
            | false, _ -> CardsFoldPolicy.None

        { FoldPolicy = foldPolicy }

module FilterDeriveParser =

    let parse (reader: TokenReader) (pageId: string) =
        let target = reader.ReadIdent()
        if not (reader.TryKeyword "from") then
            raise (DashSpecParseException($"Page '{pageId}': derive requires 'from <filter>'."))
        let source = reader.ReadIdent()
        let grainFilter =
            if reader.TryKeyword "grain" then Some(reader.ReadIdent())
            else None
        reader.SkipNewlines()
        { TargetFilter = target; SourceFilter = source; GrainFilterName = grainFilter }

module ToolbarBoardFactory =

    let fromFilterNames (names: System.Collections.Generic.IReadOnlyList<string>) =
        if names.Count = 0 then
            raise (DashSpecParseException("toolbar requires at least one filter name."))
        { Entries = [| CardRow names |] :> System.Collections.Generic.IReadOnlyList<_>; ModuleScope = None }
