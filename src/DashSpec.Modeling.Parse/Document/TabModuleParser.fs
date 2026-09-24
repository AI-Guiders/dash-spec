namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Lexing

module TabModuleParser =

    let private readTabDirective (reader: TokenReader) =
        reader.Expect TokenKind.At
        reader.ExpectKeyword "tab"
        reader.ReadIdent()

    let parseEmbedded
        (text: string)
        (expectedTabId: string)
        (specDirectory: string option)
        (parentFilters: IReadOnlyList<FilterDefinition> option)
        =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Text is required."
        if String.IsNullOrWhiteSpace expectedTabId then
            invalidArg "expectedTabId" "Expected tab id is required."

        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        let tabId = readTabDirective reader

        if not (String.Equals(tabId, expectedTabId, StringComparison.OrdinalIgnoreCase)) then
            raise (DashSpecParseException($"Tab dashspec for '{expectedTabId}' must declare @tab '{expectedTabId}', found '{tabId}'."))

        let shell = DashboardShellContext DashboardShellMode.TabModuleEmbedded
        shell.SpecDirectory <- specDirectory
        shell.TabModuleId <- Some tabId
        shell.ParentFilters <- parentFilters

        reader.SkipNewlines()
        while not reader.IsEof do
            if not (DashboardShellParser.tryParseStatement reader shell) then
                if reader.IsEof then () else raise (reader.Unexpected())

        if shell.Cards.Count = 0 then
            raise (DashSpecParseException($"Tab module '{tabId}' must declare at least one card."))

        { TabId = tabId
          Label = shell.TabModuleLabel
          Filters = shell.ExportedTabLocalFilters
          Cards = shell.Cards :> IReadOnlyList<_>
          LayoutBoard = shell.LayoutBoard
          ModuleDiagrams = None
          ModuleChartChromePresets = None
          ModuleTooltips = None
          Pages = if shell.Pages.Count = 0 then None else Some(shell.Pages :> IReadOnlyList<_>) }

    let composeStandalone (text: string) (specDirectory: string option) =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Text is required."

        let reader = ParserUtilities.createReader text
        reader.SkipFileDirectives()
        let sqlDialect = DashboardParser.readSqlDialect text
        let diagramLibraryPath = reader.ConsumedDiagramLibraryPath
        let palettePath = reader.ConsumedPalettePath

        let tabId = readTabDirective reader
        let shell = DashboardShellContext DashboardShellMode.TabModuleStandalone
        shell.SpecDirectory <- specDirectory
        shell.TabModuleId <- Some tabId

        reader.SkipNewlines()
        while not reader.IsEof do
            if not (DashboardShellParser.tryParseStatement reader shell) then
                raise (reader.Unexpected())

        if shell.Cards.Count = 0 then
            raise (DashSpecParseException($"Standalone @tab '{tabId}' must declare at least one card."))

        let title = shell.TabModuleLabel |> Option.defaultValue tabId
        let tabs: IReadOnlyList<TabDefinition> =
            [| { Id = tabId
                 Label = Some title
                 CardIds = shell.Cards |> Seq.map (fun c -> c.Id) |> Seq.toArray
                 DashspecPath = None
                 LayoutBoard = shell.LayoutBoard } |]

        let cards = TabParser.assignTabs (shell.Cards :> IReadOnlyList<_>) tabs
        let dashboardFilters =
            ToolbarPlacementResolver.resolveFilterNames (shell.Filters :> IReadOnlyList<_>) (shell.DashboardFilters :> IReadOnlyList<_>) shell.ToolbarBoard

        let document =
            { Id = tabId
              Title = title
              ConnectorId = shell.ConnectorId
              SqlDialect = sqlDialect
              DiagramLibraryPath = diagramLibraryPath
              PalettePath = palettePath
              ColorPalette = shell.ColorPalette
              Layout = shell.Layout
              FiltersChrome = shell.FiltersChrome
              Filters = shell.Filters :> IReadOnlyList<_>
              DashboardFilters = dashboardFilters
              Tabs = tabs
              Cards = cards
              ToolbarBoard = shell.ToolbarBoard
              ModuleExtensions = None
              ModuleDiagrams = None
              ModuleChartChromePresets = None
              ModuleTooltips = None
              Pages = if shell.Pages.Count = 0 then None else Some(shell.Pages :> IReadOnlyList<_>)
              CommandAliases =
                  if shell.CommandAliases.Count = 0 then
                      None
                  else
                      Some(shell.CommandAliases :> IReadOnlyDictionary<_, _>)
              FormatDefaults = shell.FormatDefaults }

        DashboardValidator.validate document
        document
