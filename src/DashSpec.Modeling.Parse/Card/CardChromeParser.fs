namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing

module CardChromeParser =

    let parse (reader: TokenReader) (cardId: string) =
        BlockSyntax.beginBlock reader
        let mutable boundFilters = CardBoundFilterChrome.Chips

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
            else
                raise (reader.Unexpected "chrome property")

        BlockSyntax.expectBlockEnd reader "chrome" None
        { BoundFilters = boundFilters }

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
        { Rows = [| names |] :> IReadOnlyList<_>; ModuleScope = None }
