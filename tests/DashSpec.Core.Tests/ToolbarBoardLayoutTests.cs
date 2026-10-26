using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>Toolbar board geometry ([ADR-0111] §2 recursion): the scope module's grid sets
/// the tracks, weights place filters, and `[]` is a rubber gap — shell semantics everywhere.</summary>
public class ToolbarBoardLayoutTests
{
    private static LayoutBoardDefinition Board(string[][] rows, int gridColumns = 0)
    {
        var board = LayoutBoardDefinition.FromCardRows(rows, LayoutScope.Toolbar);
        board.GridColumns = gridColumns;
        return board;
    }

    private static DashboardDocument Document(LayoutBoardDefinition? toolbarBoard) =>
        DocumentTestData.Create(
            "t",
            "T",
            connectorId: null,
            sqlDialect: SqlDialect.TSql,
            diagramLibraryPath: null,
            palette: null,
            colorPalette: null,
            layout: new LayoutDefinition(12),
            filtersChrome: TestModelDefaults.FiltersChrome,
            filters:
            [
                FilterTestData.Create(FilterKind.Date, "usage_date", null, null),
                FilterTestData.Create(FilterKind.Field, "program", null, null),
            ],
            dashboardFilters: ["usage_date", "program"],
            tabs: [],
            cards: [],
            toolbarBoard,
            pages: []);

    [Fact]
    public void PlaceRow_rubber_cell_consumes_its_share_and_places_nothing()
    {
        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);

        LayoutBoardRowPlacer.PlaceRow(
            ["usage_date", "[]", "program"],
            1,
            12,
            "Test",
            name => name,
            result);

        Assert.Equal(2, result.Count);
        Assert.Equal(new PlacementDefinition(1, 1, 4), result["usage_date"]);
        Assert.Equal(new PlacementDefinition(1, 9, 4), result["program"]);
    }

    [Fact]
    public void PlaceRow_lone_rubber_cell_places_nothing()
    {
        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);

        LayoutBoardRowPlacer.PlaceRow(["[]"], 1, 12, "Test", name => name, result);

        Assert.Empty(result);
    }

    [Fact]
    public void Compact_toolbar_board_grid_sets_the_tracks()
    {
        var document = Document(Board([["usage_date", "program"]], gridColumns: 5));

        var placements = ToolbarLayoutCompactor.Compact(document);

        Assert.Equal(2, placements.Count);
        Assert.Equal(new PlacementDefinition(1, 1, 2), placements["usage_date"]);
        Assert.Equal(new PlacementDefinition(1, 3, 3), placements["program"]);
    }

    [Fact]
    public void Compact_document_grid_stays_the_default_without_a_board_grid()
    {
        var document = Document(Board([["usage_date", "program"]]));

        var placements = ToolbarLayoutCompactor.Compact(document);

        Assert.Equal(new PlacementDefinition(1, 1, 6), placements["usage_date"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), placements["program"]);
    }

    [Fact]
    public void CompactVisible_rubber_cell_leaves_a_gap_between_filters()
    {
        var document = Document(Board([["usage_date", "[]", "program"]]));
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "usage_date", "program" };

        var placements = ToolbarLayoutCompactor.CompactVisible(document, visible, Board([["usage_date", "[]", "program"]]));

        Assert.Equal(2, placements.Count);
        Assert.Equal(new PlacementDefinition(1, 1, 4), placements["usage_date"]);
        Assert.Equal(new PlacementDefinition(1, 9, 4), placements["program"]);
    }
}
