namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.Diagram
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Include
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Toolbar

module rec DocumentModuleParser =

    type private ModuleShellResult =
        { Shell: DashboardShellContext
          SqlDialect: SqlDialect
          PalettePath: string option
          DiagramLibraryPath: string option
          ReportTitle: string option }

    let private exportModuleDiagrams (includes: ModuleIncludeState) =
        if includes.Diagrams.Count = 0 then
            DashboardDocument.emptyModuleDiagrams
        else
            includes.Diagrams
            |> Seq.map (fun kv ->
                let fragment = kv.Value
                let diagram =
                    match fragment.Diagram with
                    | Some d -> d
                    | None -> raise (DashSpecParseException($"Diagram include '{kv.Key}' has no kind block."))

                kv.Key,
                { Diagram = diagram
                  Presentation = fragment.Presentation
                  SeriesTransform = fragment.SeriesTransform
                  Inspect = fragment.Inspect
                  Tooltip = None })
            |> dict
            |> fun map -> Dictionary<string, ModuleDiagramDefinition>(map, StringComparer.OrdinalIgnoreCase) :> IReadOnlyDictionary<_, _>

    let isBlockModuleFormat (text: string) =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Text is required."

        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        reader.SkipNewlines()

        if not (reader.IsAt TokenKind.At) then false
        else
            reader.Advance()

            if not (reader.TryKeyword "dashboard") && not (reader.TryKeyword "tab") then false
            else
                reader.ReadIdent() |> ignore
                reader.SkipNewlines()

                if reader.IsAt TokenKind.LBrace then
                    raise (DashSpecParseException("Brace module format removed; use @dashboard id or @tab id with end-block body."))
                else
                    not reader.IsEof

    let private composeTabStandalone (reader: TokenReader) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        let tabId = reader.ReadIdent()
        let result = parseTabModuleShell reader tabId DashboardShellMode.TabModuleStandalone specDirectory None ReportBodyMode.TabStandalone parseOptions

        if result.Shell.Cards.Count = 0 then
            raise (DashSpecParseException($"Standalone @tab '{tabId}' must declare at least one card."))

        let title =
            result.ReportTitle
            |> Option.orElse result.Shell.TabModuleLabel
            |> Option.defaultValue tabId

        let tabs =
            [ { Id = tabId
                Label = Some title
                CardIds = result.Shell.Cards |> Seq.map (fun c -> c.Id) |> Seq.toList :> IReadOnlyList<_>
                DashspecPath = None
                LayoutBoard = result.Shell.LayoutBoard } ]

        let cards = TabParser.assignTabs (result.Shell.Cards :> IReadOnlyList<_>) tabs

        let dashboardFilters =
            ToolbarPlacementResolver.resolveFilterNames
                (result.Shell.Filters :> IReadOnlyList<_>)
                (result.Shell.DashboardFilters :> IReadOnlyList<_>)
                result.Shell.ToolbarBoard

        let document =
            { Id = tabId
              Title = title
              ConnectorId = result.Shell.ConnectorId
              SqlDialect = result.SqlDialect
              DiagramLibraryPath = result.DiagramLibraryPath
              PalettePath = result.PalettePath
              ColorPalette = result.Shell.ColorPalette
              Layout = result.Shell.Layout
              FiltersChrome = result.Shell.FiltersChrome
              Filters = result.Shell.Filters :> IReadOnlyList<_>
              DashboardFilters = dashboardFilters
              Tabs = tabs
              Cards = cards :> IReadOnlyList<_>
              ToolbarBoard = result.Shell.ToolbarBoard
              ModuleExtensions = Some result.Shell.ModuleExtensions
              ModuleDiagrams = Some(exportModuleDiagrams result.Shell.Includes)
              ModuleChartChromePresets = Some(result.Shell.Includes.ExportChartChromePresets())
              ModuleTooltips = Some(result.Shell.Includes.ExportTooltips())
              Pages = Some(result.Shell.Pages :> IReadOnlyList<_>)
              CommandAliases = Some(result.Shell.CommandAliases :> IReadOnlyDictionary<_, _>)
              FormatDefaults = result.Shell.FormatDefaults }

        DashboardValidator.validate document
        document

    let private parseDashboard (reader: TokenReader) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        let dashboardId = reader.ReadIdent()
        let (dashShell: DashboardShellContext), sqlDialect, palettePath, diagramLibraryPath, (reportTitle: string option) =
            parseDashboardShell reader dashboardId specDirectory parseOptions

        if reportTitle.IsNone || String.IsNullOrWhiteSpace (reportTitle.Value) then
            raise (DashSpecParseException($"@dashboard '{dashboardId}' report requires a title string."))

        let cards = TabParser.assignTabs (dashShell.Cards :> IReadOnlyList<_>) (dashShell.Tabs :> IReadOnlyList<_>)

        let dashboardFilters =
            ToolbarPlacementResolver.resolveFilterNames
                (dashShell.Filters :> IReadOnlyList<_>)
                (dashShell.DashboardFilters :> IReadOnlyList<_>)
                dashShell.ToolbarBoard

        { Id = dashboardId
          Title = reportTitle.Value
          ConnectorId = dashShell.ConnectorId
          SqlDialect = sqlDialect
          DiagramLibraryPath = diagramLibraryPath
          PalettePath = palettePath
          ColorPalette = dashShell.ColorPalette
          Layout = dashShell.Layout
          FiltersChrome = dashShell.FiltersChrome
          Filters = dashShell.Filters :> IReadOnlyList<_>
          DashboardFilters = dashboardFilters
          Tabs = dashShell.Tabs :> IReadOnlyList<_>
          Cards = cards :> IReadOnlyList<_>
          ToolbarBoard = dashShell.ToolbarBoard
          ModuleExtensions = Some dashShell.ModuleExtensions
          ModuleDiagrams = Some(exportModuleDiagrams dashShell.Includes)
          ModuleChartChromePresets = Some(dashShell.Includes.ExportChartChromePresets())
          ModuleTooltips = Some(dashShell.Includes.ExportTooltips())
          Pages = Some(dashShell.Pages :> IReadOnlyList<_>)
          CommandAliases = Some(dashShell.CommandAliases :> IReadOnlyDictionary<_, _>)
          FormatDefaults = dashShell.FormatDefaults }

    let parseDocument (text: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Text is required."

        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()
        reader.Expect TokenKind.At

        if reader.TryKeyword "tab" then
            composeTabStandalone reader specDirectory parseOptions
        elif reader.TryKeyword "dashboard" then
            let document = parseDashboard reader specDirectory parseOptions

            if document.Tabs |> Seq.forall (fun t -> String.IsNullOrWhiteSpace(Option.defaultValue "" t.DashspecPath)) then
                DashboardValidator.validate document
                document
            elif specDirectory.IsNone || String.IsNullOrWhiteSpace specDirectory.Value then
                raise (DashSpecParseException("Tab dashspec references require specDirectory when parsing."))
            else
                DashboardValidator.validate document
                document
        else
            raise (DashSpecParseException("Block module must start with @dashboard or @tab."))

    let parseDocumentDefault (text: string) (specDirectory: string option) =
        parseDocument text specDirectory DashSpecParseOptions.defaultOptions

    let parseTabEmbedded
        (text: string)
        (expectedTabId: string)
        (specDirectory: string option)
        (parentFilters: IReadOnlyList<FilterDefinition> option)
        (parseOptions: DashSpecParseOptions)
        =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Text is required."
        if String.IsNullOrWhiteSpace expectedTabId then invalidArg "expectedTabId" "Tab id is required."

        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()
        reader.Expect TokenKind.At
        reader.ExpectKeyword "tab"
        let tabId = reader.ReadIdent()

        if not (String.Equals(tabId, expectedTabId, StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Tab dashspec for '{expectedTabId}' must declare @tab '{expectedTabId}', found '{tabId}'."))

        let result =
            parseTabModuleShell reader tabId DashboardShellMode.TabModuleEmbedded specDirectory parentFilters ReportBodyMode.TabEmbedded parseOptions

        if result.Shell.Cards.Count = 0 then
            raise (DashSpecParseException($"Tab module '{tabId}' must declare at least one card."))

        { TabId = tabId
          Label = result.Shell.TabModuleLabel
          Filters = result.Shell.ExportedTabLocalFilters
          Cards = result.Shell.Cards :> IReadOnlyList<_>
          LayoutBoard = result.Shell.LayoutBoard
          ModuleDiagrams = Some(exportModuleDiagrams result.Shell.Includes)
          ModuleChartChromePresets = Some(result.Shell.Includes.ExportChartChromePresets())
          ModuleTooltips = Some(result.Shell.Includes.ExportTooltips())
          Pages = Some(result.Shell.Pages :> IReadOnlyList<_>) }

    let readRuntimeManifest (text: string) =
        if not (isBlockModuleFormat text) then None
        else
            let reader = ParserUtilities.createReader text
            reader.SkipNewlines()
            reader.Expect TokenKind.At
            let isDashboard = reader.TryKeyword "dashboard"
            if not isDashboard then reader.TryKeyword "tab" |> ignore
            let moduleId = reader.ReadIdent()
            let moduleKind = if isDashboard then "dashboard" else "tab"
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()

            let rec loop () =
                if reader.IsEof || BlockSyntax.isBlockEnd reader moduleKind (Some moduleId) then None
                else
                    reader.SkipNewlines()
                    if BlockSyntax.isBlockEnd reader moduleKind (Some moduleId) then None
                    elif reader.TryKeyword "runtime" then
                        let props = PropertyBlockParser.parse reader PropertySchemas.runtime "runtime" false false
                        match props.TryGetValue "manifest" with
                        | true, value -> Some value
                        | false, _ -> None
                    elif reader.TryKeyword "configuration" || reader.TryKeyword "wiring" || reader.TryKeyword "report" then
                        None
                    elif reader.TryKeyword "extensions" then
                        skipTopLevelSection reader "extensions"
                        loop ()
                    elif reader.TryModuleInclude().IsSome then
                        reader.SkipNewlines()
                        loop ()
                    else
                        raise (reader.Unexpected())

            loop ()

    let readConfigurationValue (text: string) (key: string) =
        if not (isBlockModuleFormat text) then None
        else
            let reader = ParserUtilities.createReader text
            reader.SkipNewlines()
            reader.Expect TokenKind.At
            let isDashboard = reader.TryKeyword "dashboard"
            if not isDashboard then reader.TryKeyword "tab" |> ignore
            let moduleId = reader.ReadIdent()
            let moduleKind = if isDashboard then "dashboard" else "tab"
            BlockSyntax.beginBlock reader
            reader.SkipNewlines()

            let rec loop () =
                if reader.IsEof || BlockSyntax.isBlockEnd reader moduleKind (Some moduleId) then None
                else
                    reader.SkipNewlines()
                    if BlockSyntax.isBlockEnd reader moduleKind (Some moduleId) then None
                    elif reader.TryKeyword "configuration" then
                        let props = PropertyBlockParser.parse reader PropertySchemas.configuration "configuration" false false
                        match props.TryGetValue key with
                        | true, value -> Some value
                        | false, _ -> None
                    elif reader.TryKeyword "runtime" then
                        skipTopLevelSection reader "runtime"
                        loop ()
                    elif reader.TryKeyword "wiring" then
                        skipTopLevelSection reader "wiring"
                        loop ()
                    elif reader.TryKeyword "report" then None
                    elif reader.TryKeyword "extensions" then
                        skipTopLevelSection reader "extensions"
                        loop ()
                    elif reader.TryModuleInclude().IsSome then
                        reader.SkipNewlines()
                        loop ()
                    else
                        raise (reader.Unexpected())

            loop ()

    let private parseDashboardShell (reader: TokenReader) (dashboardId: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) : DashboardShellContext * SqlDialect * string option * string option * string option =
        let includes = ModuleIncludeState()
        let mutable moduleExtensions = { EnabledPluginIds = []; Imports = [] }
        let mutable sqlDialect = SqlDialect.TSql
        let mutable palettePath = None
        let mutable diagramLibraryPath = None
        let mutable connectorId = None
        let mutable paletteUse = None
        let mutable layout = LayoutDefinition.Default
        let mutable wiringLayoutBoard = None
        let mutable wiringToolbarBoard = None
        let mutable shell: DashboardShellContext option = None
        let mutable reportTitle = None

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not reader.IsEof && not (BlockSyntax.isBlockEnd reader "dashboard" (Some dashboardId)) do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "dashboard" (Some dashboardId) then ()
            elif
                tryParseEnvelopeSection
                    reader
                    DocumentModuleKind.Dashboard
                    specDirectory
                    includes
                    parseOptions
                    (fun v -> sqlDialect <- v)
                    (fun v -> palettePath <- v)
                    (fun v -> diagramLibraryPath <- v)
                    (fun v -> connectorId <- v)
                    (fun v -> paletteUse <- v)
                    (fun v -> layout <- v)
                    (fun v -> moduleExtensions <- v)
                    (fun lb -> wiringLayoutBoard <- Some lb)
                    (fun tb -> wiringToolbarBoard <- Some tb)
            then
                ()
            elif reader.TryKeyword "report" then
                let created =
                    createShell
                        DashboardShellMode.DashboardBody
                        specDirectory
                        None
                        None
                        includes
                        connectorId
                        paletteUse
                        layout
                        wiringLayoutBoard
                        wiringToolbarBoard
                        parseOptions
                        moduleExtensions

                shell <- Some created
                reportTitle <- readOptionalReportTitle reader
                let mutable moduleLabel = None
                parseReportBlock reader created ReportBodyMode.DashboardRoot (fun label -> moduleLabel <- Some label)
                if reportTitle.IsNone then reportTitle <- moduleLabel
            else
                raise (reader.Unexpected())

        if not reader.IsEof then
            BlockSyntax.expectBlockEnd reader "dashboard" (Some dashboardId)

        match shell with
        | None -> raise (DashSpecParseException("@dashboard module body is empty."))
        | Some s -> s, sqlDialect, palettePath, diagramLibraryPath, reportTitle

    let private parseTabModuleShell
        (reader: TokenReader)
        (tabId: string)
        (mode: DashboardShellMode)
        (specDirectory: string option)
        (parentFilters: IReadOnlyList<FilterDefinition> option)
        (reportMode: ReportBodyMode)
        (parseOptions: DashSpecParseOptions)
        =
        let includes = ModuleIncludeState()
        let mutable moduleExtensions = { EnabledPluginIds = []; Imports = [] }
        let mutable sqlDialect = SqlDialect.TSql
        let mutable palettePath = None
        let mutable diagramLibraryPath = None
        let mutable connectorId = None
        let mutable paletteUse = None
        let mutable layout = LayoutDefinition.Default
        let mutable wiringLayoutBoard = None
        let mutable wiringToolbarBoard = None
        let mutable shell: DashboardShellContext option = None
        let mutable reportTitle = None

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not reader.IsEof && not (BlockSyntax.isBlockEnd reader "tab" (Some tabId)) do
            reader.SkipNewlines()
            if BlockSyntax.isBlockEnd reader "tab" (Some tabId) then ()
            elif
                tryParseEnvelopeSection
                    reader
                    DocumentModuleKind.Tab
                    specDirectory
                    includes
                    parseOptions
                    (fun v -> sqlDialect <- v)
                    (fun v -> palettePath <- v)
                    (fun v -> diagramLibraryPath <- v)
                    (fun v -> connectorId <- v)
                    (fun v -> paletteUse <- v)
                    (fun v -> layout <- v)
                    (fun v -> moduleExtensions <- v)
                    (fun lb -> wiringLayoutBoard <- Some lb)
                    (fun tb -> wiringToolbarBoard <- Some tb)
            then
                ()
            elif reader.TryKeyword "report" then
                let created =
                    createShell
                        mode
                        specDirectory
                        (Some tabId)
                        parentFilters
                        includes
                        connectorId
                        paletteUse
                        layout
                        wiringLayoutBoard
                        wiringToolbarBoard
                        parseOptions
                        moduleExtensions

                shell <- Some created
                reportTitle <- readOptionalReportTitle reader
                let mutable moduleLabel = None
                parseReportBlock reader created reportMode (fun label -> moduleLabel <- Some label)

                if created.TabModuleLabel.IsNone then
                    created.TabModuleLabel <- moduleLabel

                if reportTitle.IsNone then reportTitle <- moduleLabel
            else
                raise (reader.Unexpected())

        if not reader.IsEof then
            BlockSyntax.expectBlockEnd reader "tab" (Some tabId)

        match shell with
        | None -> raise (DashSpecParseException($"@tab '{tabId}' module body is empty."))
        | Some s ->
            { Shell = s
              SqlDialect = sqlDialect
              PalettePath = palettePath
              DiagramLibraryPath = diagramLibraryPath
              ReportTitle = reportTitle }

    let private tryParseEnvelopeSection
        (reader: TokenReader)
        (moduleKind: DocumentModuleKind)
        (specDirectory: string option)
        (includes: ModuleIncludeState)
        (parseOptions: DashSpecParseOptions)
        (setSqlDialect: SqlDialect -> unit)
        (setPalettePath: string option -> unit)
        (setDiagramLibraryPath: string option -> unit)
        (setConnectorId: string option -> unit)
        (setPaletteUse: string option -> unit)
        (setLayout: LayoutDefinition -> unit)
        (setModuleExtensions: ModuleExtensionsDefinition -> unit)
        (setLayoutBoard: LayoutBoardDefinition -> unit)
        (setToolbarBoard: LayoutBoardDefinition -> unit)
        =
        if reader.TryKeyword "runtime" then
            let props = PropertyBlockParser.parse reader PropertySchemas.runtime "runtime" false false
            props.TryGetValue "manifest" |> ignore
            reader.SkipNewlines()
            true
        elif reader.TryKeyword "configuration" then
            let props = PropertyBlockParser.parse reader PropertySchemas.configuration "configuration" false false

            match props.TryGetValue "sqldialect" with
            | true, dialectRaw -> setSqlDialect (SqlDialectParser.parse dialectRaw)
            | false, _ -> ()

            match props.TryGetValue "palette" with
            | true, path -> setPalettePath (Some path)
            | false, _ -> ()

            match props.TryGetValue "diagramlibrary" with
            | true, path -> setDiagramLibraryPath (Some path)
            | false, _ -> ()

            reader.SkipNewlines()
            true
        else
            match reader.TryModuleInclude() with
            | Some includeReference ->
                match specDirectory with
                | None -> raise (DashSpecParseException("!include requires specDirectory when parsing."))
                | Some dir ->
                    IncludeExpander.expand includeReference dir moduleKind includes parseOptions.TolerateIncompleteIncludes
                    reader.SkipNewlines()
                    true
            | None ->
                if reader.TryKeyword "extensions" then
                    setModuleExtensions (Card.ModuleExtensionsParser.parse reader)
                    reader.SkipNewlines()
                    true
                elif reader.TryKeyword "wiring" then
                    let wiredConnector, wiredPalette, layout, layoutBoard, toolbarBoard = parseWiringBlock reader
                    setLayout layout
                    if layoutBoard.IsSome then setLayoutBoard layoutBoard.Value
                    if toolbarBoard.IsSome then setToolbarBoard toolbarBoard.Value
                    setConnectorId wiredConnector
                    setPaletteUse wiredPalette
                    reader.SkipNewlines()
                    true
                else false

    let private parseWiringBlock (reader: TokenReader) : string option * string option * LayoutDefinition * LayoutBoardDefinition option * LayoutBoardDefinition option =
        let mutable connectorId = None
        let mutable paletteUse = None
        let mutable layout = LayoutDefinition.Default
        let mutable layoutBoard = None
        let mutable toolbarBoard = None

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "wiring" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "wiring" None then ()
            elif reader.TryKeyword "use" then
                let useKind = reader.ReadIdent()
                let useId = reader.ReadIdent()

                if String.Equals(useKind, "connector", StringComparison.OrdinalIgnoreCase) then
                    connectorId <- Some useId
                elif String.Equals(useKind, "palette", StringComparison.OrdinalIgnoreCase) then
                    paletteUse <- Some useId
                else
                    raise (DashSpecParseException($"wiring use must be connector or palette, got '{useKind}'."))
            elif reader.TryKeyword "layout" then
                match reader.TryPeekIdent() with
                | Some kind when String.Equals(kind, "grid", StringComparison.OrdinalIgnoreCase) ->
                    layout <- LayoutParser.parseGrid reader
                | Some kind when String.Equals(kind, "board", StringComparison.OrdinalIgnoreCase) ->
                    reader.ReadIdent() |> ignore
                    layoutBoard <- Some(LayoutParser.parseBoard reader)
                | _ -> raise (reader.Unexpected("layout grid or layout board"))
            else
                raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "wiring" (None: string option)
        connectorId, paletteUse, layout, layoutBoard, toolbarBoard

    let private parseReportDefaultsBlock (reader: TokenReader) (shell: DashboardShellContext) (blockKeyword: string) =
        shell.FormatDefaults <-
            DefaultsBlockParser.parse reader blockKeyword shell.FormatDefaults shell.FilterDefaults

    let private parseReportBlock (reader: TokenReader) (shell: DashboardShellContext) (mode: ReportBodyMode) (setModuleLabel: string -> unit) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader "report" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "report" None then ()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                setModuleLabel (reader.ReadString())
                reader.SkipNewlines()
            elif reader.TryKeyword "standalone" then
                if mode = ReportBodyMode.TabEmbedded then
                    skipStandaloneBlock reader
                else
                    parseStandaloneBlock reader shell
            elif reader.TryKeyword "page" then
                let pageId = reader.ReadIdent()
                if String.IsNullOrWhiteSpace pageId then
                    raise (DashSpecParseException("page requires id."))
                parsePageBlock reader shell pageId
            elif reader.TryKeyword "commands" then
                for pair in CommandAliasesParser.parse reader do
                    shell.CommandAliases.[pair.Key] <- pair.Value
                reader.SkipNewlines()
            elif reader.TryKeyword "defaults" then
                parseReportDefaultsBlock reader shell "defaults"
            elif reader.TryKeyword "default" then
                parseReportDefaultsBlock reader shell "default"
            elif reader.TryKeyword "filters" then
                match reader.TryPeekIdent() with
                | Some next when
                    String.Equals(next, "dashboard", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(next, "chrome", StringComparison.OrdinalIgnoreCase)
                    ->
                    DashboardShellParser.parseFiltersChromePublic reader shell true
                    reader.SkipNewlines()
                | _ -> parseFiltersBlock reader shell mode
            elif reader.TryKeyword "toolbar" then
                DashboardShellParser.parseFiltersChromePublic reader shell true
                reader.SkipNewlines()
            elif DashboardShellParser.tryParseStatement reader shell then ()
            else
                raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "report" (None: string option)

    let private parsePageBlock (reader: TokenReader) (shell: DashboardShellContext) (pageId: string) =
        if shell.Pages |> Seq.exists (fun page -> String.Equals(page.Id, pageId, StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Report declares duplicate page id '{pageId}'."))

        let mutable pageTitle = None
        let mutable pageLayout = None
        let mutable pageToolbar = None
        let mutable usageDateDerive = None
        let pageFilterDefaults = FilterScopeDefaults.create ()

        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let previousPageId = shell.CurrentPageId
        shell.CurrentPageId <- Some pageId

        while not (BlockSyntax.isBlockEnd reader "page" (Some pageId)) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "page" (Some pageId) then ()
            elif reader.TryKeyword "title" then
                reader.Expect TokenKind.Eq
                pageTitle <- Some(reader.ReadString())
                reader.SkipNewlines()
            elif reader.TryKeyword "toolbar" then
                if reader.TryPeekIdent() |> Option.exists (fun next -> String.Equals(next, "chrome", StringComparison.OrdinalIgnoreCase)) then
                    DashboardShellParser.parseFiltersChromePublic reader shell false
                else
                    pageToolbar <- Some(Card.ToolbarBoardFactory.fromFilterNames(reader.ReadCommaListInline()))
                reader.SkipNewlines()
            elif reader.TryKeyword "derive" then
                usageDateDerive <- Some(Card.FilterDeriveParser.parse reader pageId)
            elif reader.TryKeyword "defaults" then
                DefaultsBlockParser.parse reader "defaults" ReportFormatDefaults.empty pageFilterDefaults |> ignore
            elif reader.TryKeyword "default" then
                DefaultsBlockParser.parse reader "default" ReportFormatDefaults.empty pageFilterDefaults |> ignore
            else
                match reader.TryModuleInclude() with
                | Some includeReference ->
                    pageLayout <- Some(loadPageLayoutInclude includeReference shell.SpecDirectory)
                    reader.SkipNewlines()
                | None ->
                    if reader.TryKeyword "include" then
                        let kind, reference = DiagramModuleParser.readIncludeReference reader

                        if not (String.Equals(kind, "layout", StringComparison.OrdinalIgnoreCase)) then
                            raise (DashSpecParseException($"Page '{pageId}' allows include layout only, got include {kind}."))

                        match shell.SpecDirectory with
                        | None ->
                            raise (DashSpecParseException("include layout requires spec directory when parsing (path to the .dashspec folder)."))
                        | Some specDirectory ->
                            pageLayout <- Some(LayoutModuleParser.load reference specDirectory)
                            reader.SkipNewlines()
                    elif DashboardShellParser.tryParseStatement reader shell then ()
                    else
                        raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "page" (Some pageId)
        shell.CurrentPageId <- previousPageId

        shell.Pages.Add
            { Id = pageId
              Title = pageTitle
              LayoutBoard = pageLayout
              TabId = shell.TabModuleId
              ToolbarBoard = pageToolbar
              UsageDateDerive = usageDateDerive
              FilterDefaults = FilterScopeDefaults.toReadOnly pageFilterDefaults }

        reader.SkipNewlines()

    let private loadPageLayoutInclude (reference: string) (specDirectory: string option) =
        match specDirectory with
        | None ->
            raise (DashSpecParseException("!include layout requires spec directory when parsing (path to the .dashspec folder)."))
        | Some dir ->
            let path = DashSpec.Modeling.Parse.SpecIncludeResolver.resolvePath reference dir
            let resolved =
                if path.EndsWith(".dashlayout", StringComparison.OrdinalIgnoreCase) then path
                else Path.ChangeExtension(path, ".dashlayout")

            if not (File.Exists resolved) then
                raise (FileNotFoundException($"!include not found: '{reference}'.", resolved))

            LayoutModuleParser.parseLayoutFile (File.ReadAllText resolved)

    let private parseStandaloneBlock (reader: TokenReader) (shell: DashboardShellContext) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let standaloneFilterDefaults = FilterScopeDefaults.create ()

        let resolveFilterProperty (filterName: string) (property: string) =
            FilterScopeDefaults.resolveProperty [ standaloneFilterDefaults; shell.FilterDefaults ] filterName property

        while not (BlockSyntax.isBlockEnd reader "standalone" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "standalone" None then ()
            elif reader.TryKeyword "defaults" then
                DefaultsBlockParser.parse reader "defaults" ReportFormatDefaults.empty standaloneFilterDefaults |> ignore
            elif reader.TryKeyword "default" then
                DefaultsBlockParser.parse reader "default" ReportFormatDefaults.empty standaloneFilterDefaults |> ignore
            elif reader.TryKeyword "filter" then
                shell.Filters.Add(FilterParser.parse reader resolveFilterProperty)
                reader.SkipNewlines()
            elif reader.TryKeyword "toolbar" || reader.TryKeyword "filters" then
                DashboardShellParser.parseFiltersChromePublic reader shell true
                reader.SkipNewlines()
            else
                raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "standalone" (None: string option)

    let private skipStandaloneBlock (reader: TokenReader) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let standaloneFilterDefaults = FilterScopeDefaults.create ()

        let resolveFilterProperty (filterName: string) (property: string) =
            FilterScopeDefaults.tryGet standaloneFilterDefaults filterName property

        while not (BlockSyntax.isBlockEnd reader "standalone" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "standalone" None then ()
            elif reader.TryKeyword "defaults" then
                DefaultsBlockParser.parse reader "defaults" ReportFormatDefaults.empty standaloneFilterDefaults |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "default" then
                DefaultsBlockParser.parse reader "default" ReportFormatDefaults.empty standaloneFilterDefaults |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "filter" then
                FilterParser.parse reader resolveFilterProperty |> ignore
                reader.SkipNewlines()
            elif reader.TryKeyword "toolbar" || reader.TryKeyword "filters" then
                if reader.TryKeyword "dashboard" then
                    ToolbarPlacementParser.discard reader "filters dashboard"
                elif reader.TryKeyword "chrome" then
                    FiltersChromeParser.parse reader |> ignore
                else
                    ToolbarPlacementParser.discard reader "toolbar"
                reader.SkipNewlines()
            else
                raise (reader.Unexpected())

        BlockSyntax.expectBlockEnd reader "standalone" (None: string option)

    let private parseFiltersBlock (reader: TokenReader) (shell: DashboardShellContext) (mode: ReportBodyMode) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let blockFilterDefaults = FilterScopeDefaults.create ()

        let resolveFilterProperty (filterName: string) (property: string) =
            FilterScopeDefaults.resolveProperty [ blockFilterDefaults; shell.FilterDefaults ] filterName property

        while not (BlockSyntax.isBlockEnd reader "filters" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "filters" None then ()
            elif reader.TryKeyword "defaults" then
                DefaultsBlockParser.parse reader "defaults" ReportFormatDefaults.empty blockFilterDefaults |> ignore
            elif reader.TryKeyword "default" then
                DefaultsBlockParser.parse reader "default" ReportFormatDefaults.empty blockFilterDefaults |> ignore
            elif not (reader.TryKeyword "filter") then
                raise (reader.Unexpected())
            else
                let filter = FilterParser.parse reader resolveFilterProperty

                if mode = ReportBodyMode.TabEmbedded then
                    shell.TabLocalFilters.Add filter
                else
                    shell.Filters.Add filter

                reader.SkipNewlines()

        BlockSyntax.expectBlockEnd reader "filters" (None: string option)

    let private skipTopLevelSection (reader: TokenReader) (endKind: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()

        while not (BlockSyntax.isBlockEnd reader endKind None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader endKind None then ()
            elif reader.IsAt TokenKind.LBrace then
                skipBlock reader
            else
                while not (reader.IsOnNewline()) && not reader.IsEof do
                    reader.Advance()

        BlockSyntax.expectBlockEnd reader endKind (None: string option)

    let private skipBlock (reader: TokenReader) =
        reader.Expect TokenKind.LBrace
        let mutable depth = 1

        while depth > 0 && not reader.IsEof do
            if reader.IsAt TokenKind.LBrace then
                reader.Advance()
                depth <- depth + 1
            elif reader.IsAt TokenKind.RBrace then
                reader.Advance()
                depth <- depth - 1
            else
                reader.Advance()

    let private createShell
        (mode: DashboardShellMode)
        (specDirectory: string option)
        (tabModuleId: string option)
        (parentFilters: IReadOnlyList<FilterDefinition> option)
        (includes: ModuleIncludeState)
        (connectorId: string option)
        (paletteUse: string option)
        (layout: LayoutDefinition)
        (layoutBoard: LayoutBoardDefinition option)
        (toolbarBoard: LayoutBoardDefinition option)
        (parseOptions: DashSpecParseOptions)
        (moduleExtensions: ModuleExtensionsDefinition)
        =
        if layoutBoard.IsSome && includes.LayoutBoard.IsSome then
            raise (DashSpecParseException("Tab module declares more than one card layout board."))

        if toolbarBoard.IsSome && includes.ToolbarBoard.IsSome then
            raise (DashSpecParseException("Dashboard module declares more than one toolbar layout board."))

        let shell = DashboardShellContext(mode)
        shell.SpecDirectory <- specDirectory
        shell.TabModuleId <- tabModuleId
        shell.ParentFilters <- parentFilters
        shell.ConnectorId <- connectorId
        shell.ColorPalette <- paletteUse
        shell.Layout <- layout
        shell.LayoutBoard <- layoutBoard |> Option.orElse includes.LayoutBoard
        shell.ToolbarBoard <- toolbarBoard |> Option.orElse includes.ToolbarBoard
        shell.ParseOptions <- restrictExtensionBlocksForModule parseOptions moduleExtensions
        shell.ModuleExtensions <- moduleExtensions
        shell.Includes <- includes
        shell

    let private restrictExtensionBlocksForModule (options: DashSpecParseOptions) (moduleExtensions: ModuleExtensionsDefinition) =
        if moduleExtensions.EnabledPluginIds.Count = 0 then options
        else
            let allowed = HashSet<string>(moduleExtensions.EnabledPluginIds, StringComparer.OrdinalIgnoreCase)

            let keywords =
                options.ExtensionBlockKeywords
                |> Seq.filter (fun keyword ->
                    match options.ExtensionBlockPluginIds.TryGetValue keyword with
                    | true, pluginId -> allowed.Contains pluginId
                    | false, _ -> true)
                |> Set.ofSeq
                |> fun set -> HashSet<string>(set, StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>

            { options with ExtensionBlockKeywords = keywords }

    let private readOptionalReportTitle (reader: TokenReader) =
        reader.SkipNewlines()
        if reader.CurrentKind = TokenKind.String then Some(reader.ReadString()) else None
