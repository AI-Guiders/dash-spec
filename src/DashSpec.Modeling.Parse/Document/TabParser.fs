namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing

module TabParser =

    /// Tab with a cards body is inline; a tab without one references the tab module imported under its id (`import tabs`).
    let parse (reader: TokenReader) =
        let id = reader.ReadIdent()
        let mutable label: string option = None
        if reader.TryKeywordSameLine "as" then
            label <- Some(reader.ReadString())

        if not (reader.IsOnNewline()) then
            raise (DashSpecParseException($"Tab '{id}' header must end its line."))

        reader.SkipNewlines()

        match reader.TryPeekIdent() with
        | Some("cards" | "layout") ->
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()
            let mutable cardIds: IReadOnlyList<string> = [||]
            let mutable layoutBoard: LayoutBoardDefinition option = None

            while not (BlockSyntax.isBlockEnd reader "tab" (Some id)) && not reader.IsEof do
                reader.SkipNewlines()
                if BlockSyntax.isBlockEnd reader "tab" (Some id) then ()
                elif reader.TryKeyword "cards" then
                    cardIds <- PropertyBlockParser.parseIdentListBlock reader "cards" $"tab {id} cards"
                    reader.SkipNewlines()
                elif reader.TryKeyword "layout" then
                    layoutBoard <- Some(LayoutParser.parseBoard reader)
                    reader.SkipNewlines()
                else
                    let key = reader.ReadIdent()
                    raise (DashSpecParseException($"Unknown property '{key}' in tab {id} block."))

            BlockSyntax.expectBlockEnd reader "tab" (Some id)

            if cardIds.Count = 0 then
                raise (DashSpecParseException($"Tab '{id}' requires a cards block."))

            { Id = id; Label = label; CardIds = cardIds; ModulePath = None; LayoutBoard = layoutBoard }
        | _ ->
            { Id = id; Label = label; CardIds = [||]; ModulePath = None; LayoutBoard = None }

    let parseModuleLocalBlock (reader: TokenReader) (expectedTabId: string) (allowFilters: bool) =
        let id = reader.ReadIdent()
        if not (String.Equals(id, expectedTabId, StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Tab module declares @tab '{expectedTabId}' but tab block uses '{id}'."))

        let mutable label: string option = None
        if reader.TryKeywordSameLine "as" then
            label <- Some(reader.ReadString())

        let filters = ResizeArray<FilterDefinition>()
        let mutable layoutBoard: LayoutBoardDefinition option = None

        if reader.IsOnNewline() then
            reader.SkipNewlines()

        if reader.IsEof || BlockSyntax.isBlockEnd reader "tab" (Some expectedTabId) then
            label, filters :> IReadOnlyList<_>, layoutBoard
        else
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()

            while not (BlockSyntax.isBlockEnd reader "tab" (Some expectedTabId)) && not reader.IsEof do
                reader.SkipNewlines()
                if BlockSyntax.isBlockEnd reader "tab" (Some expectedTabId) then ()
                elif reader.TryKeyword "layout" then
                    layoutBoard <- Some(LayoutParser.parseBoard reader)
                    reader.SkipNewlines()
                elif allowFilters && reader.TryKeyword "defaults" then
                    raise (DashSpecParseException($"Tab block does not support defaults; declare defaults in report or tab module."))
                elif allowFilters && reader.TryKeyword "filter" then
                    filters.Add(FilterParser.parse reader (fun _ _ -> None))
                    reader.SkipNewlines()
                else
                    let key = reader.ReadIdent()
                    raise (DashSpecParseException($"Tab module '{expectedTabId}' allows only layout and filter declarations in tab block, not '{key}'."))

            BlockSyntax.expectBlockEnd reader "tab" (Some expectedTabId)
            label, filters :> IReadOnlyList<_>, layoutBoard

    let assignTabs (resolveName: string -> string) (cards: IReadOnlyList<CardDefinition>) (tabs: IReadOnlyList<TabDefinition>) =
        if tabs.Count = 0 then
            cards |> Seq.toList
        else
            let idToTab = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            for tab in tabs do
                for token in tab.CardIds do
                    let cardId = CardLayoutRefResolver.resolve (resolveName token) cards $"Tab '{tab.Id}' cards"
                    idToTab.[cardId] <- tab.Id

            cards
            |> Seq.map (fun card ->
                { card with
                    TabId =
                        match idToTab.TryGetValue card.Id with
                        | true, tabId -> Some tabId
                        | false, _ -> None })
            |> Seq.toList
