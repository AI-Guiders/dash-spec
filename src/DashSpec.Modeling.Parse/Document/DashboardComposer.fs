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

    /// SSOT (ADR-0109): tabs may declare the same filter (same kind + same bound
    /// column) — one filter, not a conflict. Display chrome (widget/ref) stays the
    /// first declaration's; conflicting binds (different kind or column) still raise.
    let private filterDeclarationEquivalent (left: FilterDefinition) (right: FilterDefinition) =
        let resolvedColumn (filter: FilterDefinition) =
            match filter.ColumnReference with
            | Some column when not (String.IsNullOrWhiteSpace column) ->
                let raw = column.Trim()
                let idx = raw.LastIndexOf('.')
                if idx >= 0 && idx < raw.Length - 1 then raw.Substring(idx + 1) else raw
            | _ -> filter.Name

        left.Kind = right.Kind
        && String.Equals(resolvedColumn left, resolvedColumn right, StringComparison.OrdinalIgnoreCase)

    let private isTabBlockRoot (text: string) =
        let reader = ParserUtilities.createReader text
        reader.SkipNewlines()

        if not (reader.IsAt TokenKind.At) then false
        else
            reader.Advance()
            reader.TryKeyword "section"

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

    let private presentationBlocksEquivalent (left: PresentationBlock) (right: PresentationBlock) =
        left.Properties.Count = right.Properties.Count
        && left.Properties
           |> Seq.forall (fun kv ->
               match right.Properties.TryGetValue kv.Key with
               | true, value -> String.Equals(kv.Value, value, StringComparison.Ordinal)
               | false, _ -> false)

    let parse (text: string) (specDirectory: string option) (parseOptions: DashSpecParseOptions) =
        if String.IsNullOrWhiteSpace text then
            invalidArg "text" "Text is required."

        if not (DocumentModuleParser.isBlockModuleFormat text) then
            raise (DashSpecParseException("DashSpec requires block module format: module dashboard id … end dashboard, or module tab id … end tab. See ADR-0024, ADR-0036, ADR-0101."))

        let document = DocumentModuleParser.parseDocument text specDirectory parseOptions

        let merged =
            if parseOptions.MergeReferencedTabModules && document.Tabs |> Seq.exists (fun t -> t.CardIds.Count = 0) then
                match specDirectory with
                | None | Some "" -> raise (DashSpecParseException("Tab modules are resolved by import and require specDirectory when parsing."))
                | Some dir -> mergeTabModules document dir parseOptions
            else
                document

        DocumentCompilePipeline.completeDocument merged

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

        let rowTypes =
            Dictionary<string, DashSpec.Modeling.Core.RowTypeDef>(document.ResolvedRowTypes, StringComparer.OrdinalIgnoreCase)

        let mutable formatDefaults = document.FormatDefaults
        let mutable timePolicy = document.TimePolicy
        let mutable dashflow = document.Dashflow
        let mutable dashflowPath = document.DashflowPath

        for tab in document.Tabs do
            if tab.CardIds.Count > 0 then
                mergedTabs.Add tab
            else
                let modulePath =
                    match tab.ModulePath with
                    | Some path -> path
                    | None -> raise (DashSpecParseException($"Tab '{tab.Id}' has no cards and no imported tab module; declare cards or import tabs from a namespace."))

                let moduleText = File.ReadAllText modulePath

                if not (DocumentModuleParser.isBlockModuleFormat moduleText) then
                    raise (DashSpecParseException($"Tab module '{modulePath}' must use block format (module tab <id> with end tab)."))

                let tabModule =
                    DocumentModuleParser.parseTabEmbedded moduleText tab.Id (Some specDirectory) (Some(filters :> IReadOnlyList<_>)) parseOptions

                formatDefaults <- ReportFormatDefaults.merge formatDefaults tabModule.FormatDefaults
                timePolicy <- ReportTimePolicyParser.mergePolicyOptions timePolicy tabModule.TimePolicy

                for filter in tabModule.Filters do
                    match filters |> Seq.tryFind (fun f -> String.Equals(f.Name, filter.Name, StringComparison.OrdinalIgnoreCase)) with
                    | Some existing when filterDeclarationEquivalent existing filter -> ()
                    | Some existing ->
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares filter '{filter.Name}' already on parent dashboard."))
                    | None -> filters.Add filter

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
                    // Same rule as tooltips below: a section re-exporting the same preset
                    // (e.g. both parent and section import DashSpec.Std) is an idempotent
                    // merge — only a *different* definition under the same name conflicts.
                    match moduleChartChromePresets.TryGetValue pair.Key with
                    | true, existing when not (presentationBlocksEquivalent existing pair.Value) ->
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares chart chrome preset '{pair.Key}' with a different definition."))
                    | true, _ -> ()
                    | false, _ -> moduleChartChromePresets.[pair.Key] <- pair.Value

                for pair in Option.defaultValue DashboardDocument.emptyModuleTooltips tabModule.ModuleTooltips do
                    match moduleTooltips.TryGetValue pair.Key with
                    | true, existing when not (tooltipDefinitionsEquivalent existing pair.Value) ->
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares module tooltip '{pair.Key}' with a different definition."))
                    | true, _ -> ()
                    | false, _ -> moduleTooltips.[pair.Key] <- pair.Value

                for pair in Option.defaultValue DashboardDocument.emptyRowTypes tabModule.RowTypes do
                    if rowTypes.ContainsKey pair.Key then
                        raise (DashSpecParseException($"Tab module '{tab.Id}' redeclares row type '{pair.Key}'."))
                    rowTypes.[pair.Key] <- pair.Value

                match tabModule.Dashflow with
                | None -> ()
                | Some moduleFlow ->
                    match dashflow with
                    | None ->
                        dashflow <- Some moduleFlow
                        dashflowPath <- tabModule.DashflowPath
                    | Some _ ->
                        raise (DashSpecParseException($"Tab module '{tab.Id}' declares dashflow but parent dashboard already has a module flow graph."))

                let label = tab.Label |> Option.orElse tabModule.Label

                mergedTabs.Add
                    { tab with
                        Label = label
                        CardIds = tabModule.Cards |> Seq.map (fun c -> c.Id) |> Seq.toList :> IReadOnlyList<_>
                        LayoutBoard = tabModule.LayoutBoard }

        let assignedCards =
            TabParser.assignTabs
                id
                (cards :> IReadOnlyList<_>)
                (mergedTabs :> IReadOnlyList<_>)

        let merged =
            { document with
                Filters = filters :> IReadOnlyList<_>
                DashboardFilters = dashboardFilters :> IReadOnlyList<_>
                Cards = assignedCards :> IReadOnlyList<_>
                Tabs = mergedTabs :> IReadOnlyList<_>
                ModuleDiagrams = Some(moduleDiagrams :> IReadOnlyDictionary<_, _>)
                ModuleChartChromePresets = Some(moduleChartChromePresets :> IReadOnlyDictionary<_, _>)
                ModuleTooltips = Some(moduleTooltips :> IReadOnlyDictionary<_, _>)
                Pages = Some(pages :> IReadOnlyList<_>)
                FormatDefaults = formatDefaults
                TimePolicy = timePolicy
                RowTypes = Some(rowTypes :> IReadOnlyDictionary<_, _>)
                Dashflow = dashflow
                DashflowPath = dashflowPath }

        merged
