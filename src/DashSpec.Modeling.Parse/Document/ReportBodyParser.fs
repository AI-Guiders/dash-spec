namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Toolbar
open DashSpec.Modeling.Parse.Types

module ReportBodyParser =

    let private toolbarContext (ctx: ReportCompileContext) (blockName: string) =
        { AssignToolbarBoard = fun board -> ctx.AssignToolbarBoard(board, blockName)
          AddDashboardFilters = fun names -> ctx.DashboardFilters.AddRange names }

    let private parseFiltersChrome (reader: TokenReader) (ctx: ReportCompileContext) (assign: bool) =
        if reader.TryKeywordSameLine "dashboard" then
            if assign then
                ToolbarPlacementParser.parse reader (toolbarContext ctx "filters dashboard") "filters dashboard"
            else
                ToolbarPlacementParser.discard reader "filters dashboard"
        elif reader.TryKeywordSameLine "chrome" then
            let chrome = FiltersChromeParser.parse reader
            if assign then ctx.FiltersChrome <- chrome
        elif assign then
            ToolbarPlacementParser.parse reader (toolbarContext ctx "toolbar") "toolbar"
        else
            ToolbarPlacementParser.discard reader "toolbar"

    let parseFiltersChromePublic (reader: TokenReader) (ctx: ReportCompileContext) (assign: bool) =
        parseFiltersChrome reader ctx assign

    let private tryParseIncludeToolbar (reader: TokenReader) (ctx: ReportCompileContext) =
        if not (reader.TryKeyword "include") then false
        else
            let kind, reference = DiagramModuleParser.readIncludeReference reader
            if not (String.Equals(kind, "toolbar", StringComparison.OrdinalIgnoreCase)) then
                raise (DashSpecParseException($"Dashboard shell allows include toolbar only, got include {kind}."))
            match ctx.SpecDirectory with
            | None | Some "" ->
                raise (DashSpecParseException("include toolbar requires spec directory when parsing (path to the .dashspec folder)."))
            | Some specDirectory ->
                ctx.AssignToolbarBoard(LayoutModuleParser.load reference specDirectory, "include toolbar")
                reader.SkipNewlines()
                true

    let private tryParseIncludeLayout (reader: TokenReader) (ctx: ReportCompileContext) =
        if not (reader.TryKeyword "include") then false
        else
            let kind, reference = DiagramModuleParser.readIncludeReference reader
            if not (String.Equals(kind, "layout", StringComparison.OrdinalIgnoreCase)) then
                raise (DashSpecParseException($"Tab module shell allows include layout only at file top, got include {kind}."))
            match ctx.SpecDirectory with
            | None | Some "" ->
                raise (DashSpecParseException("include layout requires spec directory when parsing (path to the .dashspec folder)."))
            | Some specDirectory ->
                if ctx.LayoutBoard.IsSome then
                    raise (DashSpecParseException("Tab module declares more than one layout board."))
                ctx.AssignTabLayoutBoard(LayoutModuleParser.load reference specDirectory, "include layout")
                reader.SkipNewlines()
                true

    let private applyTabModuleBlock
        (ctx: ReportCompileContext)
        (moduleLabel: string option)
        (moduleFilters: IReadOnlyList<FilterDefinition>)
        (moduleLayout: LayoutBoardDefinition option)
        =
        ctx.TabModuleLabel <- ctx.TabModuleLabel |> Option.orElse moduleLabel

        if moduleLayout.IsSome && ctx.LayoutBoard.IsSome then
            raise (DashSpecParseException($"Tab module '{ctx.TabModuleId}' declares layout twice: include layout and tab {{ layout }}."))

        ctx.LayoutBoard <- ctx.LayoutBoard |> Option.orElse moduleLayout

        match ctx.Mode with
        | ReportCompileMode.TabStandalone -> ctx.Filters.AddRange moduleFilters
        | ReportCompileMode.TabEmbedded -> ctx.TabLocalFilters.AddRange moduleFilters
        | _ -> ()

    let private parseTabStatement (reader: TokenReader) (ctx: ReportCompileContext) =
        match ctx.Mode with
        | ReportCompileMode.Dashboard -> ctx.Tabs.Add(TabParser.parse reader)
        | ReportCompileMode.TabStandalone
        | ReportCompileMode.TabEmbedded ->
            match ctx.TabModuleId with
            | None | Some "" -> raise (DashSpecParseException("Tab module shell requires @tab id before tab block."))
            | Some tabModuleId ->
                let moduleLabel, moduleFilters, moduleLayout =
                    TabParser.parseModuleLocalBlock reader tabModuleId true
                applyTabModuleBlock ctx moduleLabel moduleFilters moduleLayout

    let rec tryParseStatement (reader: TokenReader) (ctx: ReportCompileContext) =
        if ctx.Mode = ReportCompileMode.Dashboard && tryParseIncludeToolbar reader ctx then
            true
        elif (ctx.Mode = ReportCompileMode.TabStandalone || ctx.Mode = ReportCompileMode.TabEmbedded)
             && tryParseIncludeLayout reader ctx then
            true
        elif reader.TryKeyword "connector" then
            reader.ReadIdent() |> ignore
            raise (DashSpecParseException("Data provider belongs on dashflow source nodes (use provider <id> inside source { }), not on the report module."))
        elif reader.TryKeyword "layout" then
            let grid = LayoutParser.parseGrid reader
            if ctx.Mode = ReportCompileMode.Dashboard || ctx.Mode = ReportCompileMode.TabStandalone then
                ctx.Layout <- grid
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "type" then
            let rowType = TypeModuleParser.parseTypeBlockAfterKeyword reader
            ctx.Includes.RegisterRowType rowType
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "palette" then
            let palette =
                if reader.RawKind = TokenKind.Eq then
                    reader.Advance()
                reader.ReadScalarValue()
            if ctx.Mode = ReportCompileMode.Dashboard || ctx.Mode = ReportCompileMode.TabStandalone then
                ctx.ColorPalette <- Some palette
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "filters" || reader.TryKeyword "toolbar" then
            let assign =
                ctx.Mode = ReportCompileMode.Dashboard || ctx.Mode = ReportCompileMode.TabStandalone
            parseFiltersChrome reader ctx assign
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "commands" then
            for KeyValue(alias, filterId) in CommandAliasesParser.parse reader do
                ctx.CommandAliases.[alias] <- filterId
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "defaults" then
            ctx.FormatDefaults <-
                DefaultsBlockParser.parse reader "defaults" ctx.FormatDefaults ctx.FilterDefaults
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "default" then
            ctx.FormatDefaults <-
                DefaultsBlockParser.parse reader "default" ctx.FormatDefaults ctx.FilterDefaults
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "filter" then
            let filter = FilterParser.parse reader ctx.ResolveFilterProperty
            if ctx.Mode = ReportCompileMode.TabEmbedded then
                ctx.ReportScopeFilters.Add filter
            else
                ctx.Filters.Add filter
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "tab" then
            parseTabStatement reader ctx
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "phase" then
            let phaseId = reader.ReadIdent()
            if String.IsNullOrWhiteSpace phaseId then
                raise (DashSpecParseException("phase requires id."))
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()
            let previousPhase = ctx.CurrentPhaseId
            ctx.CurrentPhaseId <- Some phaseId
            while not (BlockSyntax.isBlockEnd reader "phase" (Some phaseId)) && not reader.IsEof do
                reader.SkipNewlines()
                if BlockSyntax.isBlockEnd reader "phase" (Some phaseId) then ()
                elif not (tryParseStatement reader ctx) then
                    raise (reader.Unexpected())
            BlockSyntax.expectBlockEnd reader "phase" (Some phaseId)
            ctx.CurrentPhaseId <- previousPhase
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "card" then
            ctx.Cards.Add(
                CardParser.parse
                    reader
                    ctx.CardBindValidationFilters
                    ctx.SpecDirectory
                    (Some ctx.Includes)
                    (Some ctx.ParseOptions)
                    ctx.CurrentPhaseId
                    ctx.CurrentPageId)
            reader.SkipNewlines()
            true
        else
            false
