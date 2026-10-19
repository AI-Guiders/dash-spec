using Microsoft.FSharp.Core;

namespace DashSpec.Core.Tests;

internal static class DocumentTestData
{
    public static DashboardDocument Create(
        string id,
        string title,
        string? connectorId,
        SqlDialect sqlDialect,
        string? diagramLibraryPath,
        PaletteDefinition? palette,
        string? colorPalette,
        LayoutDefinition layout,
        FiltersChromeDefinition filtersChrome,
        IReadOnlyList<FilterDefinition> filters,
        IReadOnlyList<string> dashboardFilters,
        IReadOnlyList<TabDefinition> tabs,
        IReadOnlyList<CardDefinition> cards,
        LayoutBoardDefinition? toolbarBoard = null,
        DashSpec.Modeling.Parse.Card.ModuleExtensionsDefinition? moduleExtensions = null,
        IReadOnlyDictionary<string, ModuleDiagramDefinition>? moduleDiagrams = null,
        IReadOnlyDictionary<string, PresentationBlock>? moduleChartChromePresets = null,
        IReadOnlyDictionary<string, TooltipDefinition>? moduleTooltips = null,
        IReadOnlyList<ReportPageDefinition>? pages = null,
        IReadOnlyDictionary<string, string>? commandAliases = null,
        ReportFormatDefaults? formatDefaults = null,
        CardsChromeDefinition? cardsChrome = null,
        ReportTimePolicy? timePolicy = null,
        IReadOnlyDictionary<string, DashSpec.Modeling.Core.RowTypeDef>? rowTypes = null,
        string? dashflowPath = null,
        DashflowModuleDefinition? dashflow = null,
        DashSpec.Modeling.Parse.Document.ScopeFlowDefinition? reportScopeFlow = null,
        WiringGraph? wiringGraph = null) =>
        new()
        {
            Id = id,
            Title = title,
            ConnectorId = ToOption(connectorId),
            SqlDialect = sqlDialect,
            DiagramLibraryPath = ToOption(diagramLibraryPath),
            Palette = ToOption(palette),
            ColorPalette = ToOption(colorPalette),
            Layout = layout,
            FiltersChrome = filtersChrome,
            CardsChrome = ToOption(cardsChrome),
            Filters = filters,
            DashboardFilters = dashboardFilters,
            Tabs = tabs,
            Cards = cards,
            ToolbarBoard = ToOption(toolbarBoard),
            ModuleExtensions = ToOption(moduleExtensions),
            ModuleDiagrams = ToOption(moduleDiagrams),
            ModuleChartChromePresets = ToOption(moduleChartChromePresets),
            ModuleTooltips = ToOption(moduleTooltips),
            Pages = ToOption(pages),
            CommandAliases = ToOption(commandAliases),
            FormatDefaults = formatDefaults ?? ReportFormatDefaults.Empty,
            TimePolicy = ToOption(timePolicy),
            RowTypes = ToOption(rowTypes),
            DashflowPath = ToOption(dashflowPath),
            Dashflow = ToOption(dashflow),
            ReportScopeFlow = ToOption(reportScopeFlow),
            WiringGraph = wiringGraph ?? DashSpec.Modeling.Core.WiringGraphModule.empty,
        };

    private static FSharpOption<T> ToOption<T>(T? value) where T : class =>
        value is null ? FSharpOption<T>.None : FSharpOption<T>.Some(value);

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
