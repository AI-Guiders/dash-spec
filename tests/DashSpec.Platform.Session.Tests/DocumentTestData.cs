using Microsoft.FSharp.Core;

namespace DashSpec.Platform.Session.Tests;

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
        IReadOnlyList<CardDefinition> cards) =>
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
            CardsChrome = FSharpOption<CardsChromeDefinition>.None,
            Filters = filters,
            DashboardFilters = dashboardFilters,
            Tabs = tabs,
            Cards = cards,
            ToolbarBoard = FSharpOption<LayoutBoardDefinition>.None,
            ModuleExtensions = FSharpOption<DashSpec.Modeling.Parse.Card.ModuleExtensionsDefinition>.None,
            ModuleDiagrams = FSharpOption<IReadOnlyDictionary<string, DashSpec.Modeling.Parse.Document.ModuleDiagramDefinition>>.None,
            ModuleChartChromePresets = FSharpOption<IReadOnlyDictionary<string, PresentationBlock>>.None,
            ModuleTooltips = FSharpOption<IReadOnlyDictionary<string, TooltipDefinition>>.None,
            Pages = FSharpOption<IReadOnlyList<DashSpec.Modeling.Parse.Document.ReportPageDefinition>>.None,
            CommandAliases = FSharpOption<IReadOnlyDictionary<string, string>>.None,
            FormatDefaults = new DashSpec.Modeling.Parse.Document.ReportFormatDefaults(),
            TimePolicy = FSharpOption<DashSpec.Modeling.Parse.Document.ReportTimePolicy>.None,
            RowTypes = FSharpOption<IReadOnlyDictionary<string, DashSpec.Modeling.Core.RowTypeDef>>.None,
            DashflowPath = FSharpOption<string>.None,
            Dashflow = FSharpOption<DashSpec.Modeling.Parse.DataFlow.DashflowModule>.None,
            ReportScopeFlow = FSharpOption<DashSpec.Modeling.Parse.Document.ScopeFlowDefinition>.None,
            WiringGraph = DashSpec.Modeling.Core.WiringGraphModule.empty,
        };

    private static FSharpOption<T> ToOption<T>(T? value) where T : class =>
        value is null ? FSharpOption<T>.None : FSharpOption<T>.Some(value);

    private static FSharpOption<string> ToOption(string? value) =>
        value is null ? FSharpOption<string>.None : FSharpOption<string>.Some(value);
}
