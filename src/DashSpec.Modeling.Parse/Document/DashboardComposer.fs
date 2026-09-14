namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Card
open DashSpec.Modeling.Parse.Filter
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Presentation
open DashSpec.Modeling.Parse.Tooltip

module rec DashboardComposer =

    let private isTabBlockRoot (text: string) =
        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()

        if not (reader.IsAt TokenKind.At) then false
        else
            reader.Advance()
            reader.TryKeyword "tab"

    let private insertTabModuleDashboardFilter (dashboardFilters: ResizeArray<string>) (filterName: string) =
        if String.Equals(filterName, "period_start", StringComparison.OrdinalIgnoreCase) then
            let grainIndex =
                dashboardFilters.FindIndex(fun name -> String.Equals(name, "period_grain", StringComparison.OrdinalIgnoreCase))

            if grainIndex >= 0 then
                dashboardFilters.Insert(grainIndex + 1, filterName)
            else
                dashboardFilters.Add filterName
        else
            dashboardFilters.Add filterName

    let private tooltipDefinitionsEquivalent (left: TooltipDefinition) (right: TooltipDefinition) =
        String.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase)
        && String.Equals(left.Template, right.Template, StringComparison.Ordinal)
        && left.Variables.Count = right.Variables.Count
        && left.Variables
           |> Seq.forall (fun kv ->
               match right.Variables.TryGetValue kv.Key with
               | true, value -> String.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase)
               | false, _ -> false)

    let parse (text: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Text is required."

        if not (DocumentModuleParser.isBlockModuleFormat text) then
            raise (DashSpecParseException("DashSpec requires block module format: @dashboard id { … }, @tab id { … }, or @tab id with end-block body. See ADR-0024 and ADR-0036."))

        let document = DocumentModuleParser.parseDocument text specDirectory parseOptions

        if
            parseOptions.MergeReferencedTabModules
            && document.Tabs |> Seq.exists (fun t -> not (String.IsNullOrWhiteSpace(Option.defaultValue "" t.DashspecPath)))
        then
            match specDirectory with
            | None | Some "" -> raise (DashSpecParseException("Tab dashspec references require specDirectory when parsing."))
            | Some dir -> mergeTabModules document dir parseOptions
        else
            document

    let parseDefault (text: string) (specDirectory: string option) =
        parse text specDirectory DashSpecParseOptions.defaultOptions

    let isTabRootDocument (text: string) =
        DocumentModuleParser.isBlockModuleFormat text && isTabBlockRoot text

    let mergeTabModules (document: DashboardDocument) (specDirectory: string) (parseOptions: DashSpecParseOptions) =
        let filters = ResizeArray<FilterDefinition>(document.Filters)
        let dashboardFilters = ResizeArray<string>(document.DashboardFilters)
        let cards = ResizeArray<CardDefinition>(document.Cards)
        let mergedTabs = ResizeArray<TabDefinition>()
        let pages =
            match document.Pages with
            | None -> ResizeArray<ReportPageDefinition>()
            | Some existing -> ResizeArray(existing)

        let moduleDiagrams =
            Dictionary<string, ModuleDiagramDefinition>(document.ResolvedModuleDiagrams, StringComparer.OrdinalIgnoreCase)

        let moduleChartChromePresets =
            Dictionary<string, PresentationBlock>(document.ResolvedChartChromePresets, StringComparer.OrdinalIgnoreCase)

        let moduleTooltips =
            Dictionary<string, TooltipDefinition>(document.ResolvedModuleTooltips, StringComparer.OrdinalIgnoreCase)

        for tab in document.Tabs do
            if String.IsNullOrWhiteSpace(Option.defaultValue "" tab.DashspecPath) then
                mergedTabs.Add tab
            else
                let modulePath = Path.GetFullPath(Path.Combine(specDirectory, tab.DashspecPath.Value))

                if not (File.Exists modulePath) then
                    raise (FileNotFoundException($"Tab '{tab.Id}' dashspec not found: '{tab.DashspecPath.Value}' (resolved: {modulePath}).", modulePath))

                let moduleText = File.ReadAllText modulePath

                if not (DocumentModuleParser.isBlockModuleFormat moduleText) then
                    raise (DashSpecParseException($"Tab module '{tab.DashspecPath.Value}' must use block format (@tab id {{ … }} or end-block @tab id)."))

                let tabModule =
                    DocumentModuleParser.parseTabEmbedded moduleText tab.Id (Some specDirectory) (Some(filters :> IReadOnlyList<_>)) parseOptions

                for filter in tabModule.Filters do
                    if filters |> Seq.exists (fun f -> String.Equals(f.Name, filter.Name, StringComparison.OrdinalIgnoreCase)) then
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares filter '{filter.Name}' already on parent dashboard."))

                    filters.Add filter

                    if filter.Kind <> FilterKind.Top
                       && not (dashboardFilters |> Seq.exists (fun name -> String.Equals(name, filter.Name, StringComparison.OrdinalIgnoreCase))) then
                        insertTabModuleDashboardFilter dashboardFilters filter.Name

                for card in tabModule.Cards do
                    if cards |> Seq.exists (fun c -> String.Equals(c.Id, card.Id, StringComparison.OrdinalIgnoreCase)) then
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares card '{card.Id}' already on parent dashboard."))

                    cards.Add card

                for page in Option.defaultValue (ResizeArray<ReportPageDefinition>() :> IReadOnlyList<_>) tabModule.Pages do
                    let pageWithTab =
                        if String.IsNullOrWhiteSpace(Option.defaultValue "" page.TabId) then
                            { page with TabId = Some tab.Id }
                        else
                            page

                    if
                        pages
                        |> Seq.exists (fun p ->
                            String.Equals(p.Id, pageWithTab.Id, StringComparison.OrdinalIgnoreCase)
                            && String.Equals(Option.defaultValue "" p.TabId, Option.defaultValue "" pageWithTab.TabId, StringComparison.OrdinalIgnoreCase))
                    then
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares page '{pageWithTab.Id}' already on parent dashboard."))

                    pages.Add pageWithTab

                for pair in Option.defaultValue DashboardDocument.emptyModuleDiagrams tabModule.ModuleDiagrams do
                    if moduleDiagrams.ContainsKey pair.Key then
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares module diagram preset '{pair.Key}'."))

                    moduleDiagrams.[pair.Key] <- pair.Value

                for pair in Option.defaultValue DashboardDocument.emptyModuleChartChromePresets tabModule.ModuleChartChromePresets do
                    if moduleChartChromePresets.ContainsKey pair.Key then
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares chart chrome preset '{pair.Key}'."))

                    moduleChartChromePresets.[pair.Key] <- pair.Value

                for pair in Option.defaultValue DashboardDocument.emptyModuleTooltips tabModule.ModuleTooltips do
                    match moduleTooltips.TryGetValue pair.Key with
                    | true, existing when not (tooltipDefinitionsEquivalent existing pair.Value) ->
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares module tooltip '{pair.Key}' with a different definition."))
                    | true, _ -> ()
                    | false, _ -> moduleTooltips.[pair.Key] <- pair.Value

                let label = tab.Label |> Option.orElse tabModule.Label

                mergedTabs.Add
                    { tab with
                        Label = label
                        CardIds = tabModule.Cards |> Seq.map (fun c -> c.Id) |> Seq.toList :> IReadOnlyList<_>
                        LayoutBoard = tabModule.LayoutBoard }

        let assignedCards = TabParser.assignTabs (cards :> IReadOnlyList<_>) (mergedTabs :> IReadOnlyList<_>)

        let merged =
            { document with
                Filters = filters :> IReadOnlyList<_>
                DashboardFilters = dashboardFilters :> IReadOnlyList<_>
                Cards = assignedCards :> IReadOnlyList<_>
                Tabs = mergedTabs :> IReadOnlyList<_>
                ModuleDiagrams = Some(moduleDiagrams :> IReadOnlyDictionary<_, _>)
                ModuleChartChromePresets = Some(moduleChartChromePresets :> IReadOnlyDictionary<_, _>)
                ModuleTooltips = Some(moduleTooltips :> IReadOnlyDictionary<_, _>)
                Pages = Some(pages :> IReadOnlyList<_>) }

        DashboardValidator.validate merged
        merged
