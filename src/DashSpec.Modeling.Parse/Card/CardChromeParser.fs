namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
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
        BlockSyntax.beginBlock reader
        let mutable boundFilters = CardBoundFilterChrome.Chips
        let mutable hideTitle = false
        let mutable fold = CardFoldMode.None

        while not (BlockSyntax.isBlockEnd reader "chrome" None) do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "chrome" None then ()
            elif reader.TryKeyword "bound_filters" then
                reader.Expect TokenKind.Eq
                boundFilters <-
                    match reader.ReadIdent().Trim().ToLowerInvariant() with
                    | "hidden" -> CardBoundFilterChrome.Hidden
                    | "toolbar_only" | "toolbar-only" | "toolbaronly" -> CardBoundFilterChrome.ToolbarOnly
                    | "chips" -> CardBoundFilterChrome.Chips
                    | raw ->
                        raise (DashSpecParseException($"Card '{cardId}': chrome bound_filters must be chips, hidden, or toolbar_only; got '{raw}'."))
                reader.SkipNewlines()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                hideTitle <-
                    match reader.ReadIdent().Trim().ToLowerInvariant() with
                    | "hidden" | "omit" | "suppress" -> true
                    | raw ->
                        raise (DashSpecParseException($"Card '{cardId}': chrome title must be hidden, omit, or suppress; got '{raw}'."))
                reader.SkipNewlines()
            elif reader.TryKeyword "fold" then
                reader.Expect TokenKind.Eq
                fold <- parseFoldMode cardId (reader.ReadIdent())
                reader.SkipNewlines()
            else
                raise (reader.Unexpected "chrome property")

        BlockSyntax.expectBlockEnd reader "chrome" None
        { BoundFilters = boundFilters; HideTitle = hideTitle; Fold = fold }

module CardsChromeParser =

    let parse (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let mutable foldPolicy = CardsFoldPolicy.None

        while not (BlockSyntax.isBlockEnd reader "chrome" None) && not reader.IsEof do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "chrome" None then ()
            elif reader.TryKeyword "fold" then
                reader.Expect TokenKind.Eq
                let raw = reader.ReadIdent().Trim().ToLowerInvariant()
                foldPolicy <-
                    match raw with
                    | "none" -> CardsFoldPolicy.None
                    | "focus_single" | "focus-single" | "focus" -> CardsFoldPolicy.FocusSingle
                    | _ ->
                        raise (DashSpecParseException($"cards chrome fold must be none or focus_single; got '{raw}'."))
                reader.SkipNewlines()
            else
                raise (reader.Unexpected "cards chrome property")

        BlockSyntax.expectBlockEnd reader "chrome" None
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

    let fromFilterNames (names: IReadOnlyList<string>) =
        if names.Count = 0 then
            raise (DashSpecParseException("toolbar requires at least one filter name."))
        { Entries = [| CardRow names |] :> IReadOnlyList<_>; ModuleScope = None }
