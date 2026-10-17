using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public class ToolbarLayoutCompactorTests
{
    [Fact]
    public void CompactVisible_prefers_page_toolbar_board_over_report_board()
    {
        var reportBoard = LayoutBoardDefinition.FromCardRows(
            [["usage_date", "app_name"]],
            LayoutScope.Toolbar);
        var pageBoard = LayoutBoardDefinition.FromCardRows(
            [["period_grain", "period_start"]],
            LayoutScope.Toolbar);

        var document = new DashboardDocument(
            "t",
            "T",
            ConnectorId: null,
            SqlDialect: SqlDialect.TSql,
            DiagramLibraryPath: null,
            Palette: null,
            ColorPalette: null,
            Layout: new LayoutDefinition(12, 8),
            FiltersChrome: TestModelDefaults.FiltersChrome,
            Filters:
            [
                FilterTestData.Create(FilterKind.Date, "usage_date", null, null),
                FilterTestData.Create(FilterKind.Field, "app_name", null, null),
                FilterTestData.Create(FilterKind.Field, "period_grain", null, null),
                FilterTestData.Create(FilterKind.Date, "period_start", null, null),
            ],
            DashboardFilters: ["usage_date", "app_name"],
            Tabs: [],
            Cards: [],
            ToolbarBoard: reportBoard,
            Pages:
            [
                new ReportPageDefinition
                {
                    Id = "p",
                    Title = Microsoft.FSharp.Core.FSharpOption<string>.None,
                    LayoutBoard = Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None,
                    TabId = Microsoft.FSharp.Core.FSharpOption<string>.None,
                    ToolbarBoard = Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.Some(pageBoard),
                    UsageDateDerive = Microsoft.FSharp.Core.FSharpOption<FilterDeriveDefinition>.None,
                    FilterDefaults = Microsoft.FSharp.Core.FSharpOption<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>.None,
                    DisplayBindings = Microsoft.FSharp.Core.FSharpOption<IReadOnlyDictionary<string, string>>.None,
                    ScopeFlow = Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Document.ScopeFlowDefinition>.None,
                },
            ]);

        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "period_grain",
            "period_start",
        };

        var placements = ToolbarLayoutCompactor.CompactVisible(document, visible, pageBoard);

        Assert.Equal(2, placements.Count);
        Assert.True(placements.ContainsKey("period_grain"));
        Assert.True(placements.ContainsKey("period_start"));
        Assert.False(placements.ContainsKey("usage_date"));
        Assert.Equal(1, placements["period_grain"].Row);
        Assert.Equal(1, placements["period_start"].Row);
    }
}
