using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class LayoutBoardTests
{
    [Fact]
    public void Parse_card_ref_and_tab_layout_board()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
@tab demo
  connect
  layout board
  [ Q E ]
  [ T F ]
  end layout board
  end connect
  report
  title = "Demo"
  card peak_by_app as "Peak" ref Q
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card peak_apps as "Apps" ref E
  diagram heatmap
  x = a y
  value = c
  end heatmap
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card idle as "Idle" ref T
  diagram heatmap
  x = a y
  value = c
  end heatmap
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card utilization as "Util" ref F
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  end report
end tab
""");

        Assert.Equal("Q", doc.Cards[0].LayoutRef);
        Assert.NotNull(doc.Tabs[0].LayoutBoard);
        Assert.Equal(2, doc.Tabs[0].LayoutBoard!.RowCount);
        Assert.Equal(2, doc.Tabs[0].LayoutBoard!.ColumnCount);
    }

    [Fact]
    public void TabLayoutBoardResolver_places_2x2_grid()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
@tab demo
  connect
  layout board
  [ Q E ]
  [ T F ]
  end layout board
  end connect
  report
  title = "demo"
  card a as "A" ref Q
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card b as "B" ref E
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card c as "C" ref T
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card d as "D" ref F
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  end report
end tab
""");

        var layout = TabLayoutCompactor.Compact(doc, "demo");

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["a"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["b"]);
        Assert.Equal(new PlacementDefinition(2, 1, 6), layout["c"]);
        Assert.Equal(new PlacementDefinition(2, 7, 6), layout["d"]);
    }

    [Fact]
    public void TabLayoutBoardResolver_single_cell_row_is_full_width()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
@tab demo
  connect
  layout board
  [ Q W ]
  [ E ]
  end layout board
  end connect
  report
  title = "demo"
  card a as "A" ref Q
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card b as "B" ref W
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card c as "C" ref E
  diagram heatmap
  x = a y
  value = c
  end heatmap
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  end report
end tab
""");

        var layout = TabLayoutCompactor.Compact(doc, "demo");

        Assert.Equal(6, layout["a"].Span);
        Assert.Equal(6, layout["b"].Span);
        Assert.Equal(12, layout["c"].Span);
        Assert.Equal(2, layout["c"].Row);
    }

    [Fact]
    public void TabLayoutBoardResolver_uneven_rows_distribute_per_row()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
@tab demo
  connect
  layout board
  [ Q E ]
  [ R T Y ]
  [ F ]
  end layout board
  end connect
  report
  title = "demo"
  card q as "Q" ref Q
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card e as "E" ref E
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card r as "R" ref R
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card t as "T" ref T
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card y as "Y" ref Y
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card f as "F" ref F
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  end report
end tab
""");

        Assert.Equal(3, doc.Tabs[0].LayoutBoard!.RowCount);
        Assert.Equal(3, doc.Tabs[0].LayoutBoard!.ColumnCount);

        var layout = TabLayoutCompactor.Compact(doc, "demo");

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["q"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["e"]);
        Assert.Equal(new PlacementDefinition(2, 1, 4), layout["r"]);
        Assert.Equal(new PlacementDefinition(2, 5, 4), layout["t"]);
        Assert.Equal(new PlacementDefinition(2, 9, 4), layout["y"]);
        Assert.Equal(new PlacementDefinition(3, 1, 12), layout["f"]);
    }

    [Fact]
    public void Parse_include_layout_at_tab_module_shell()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "grid.dashlayout"), """
                @layout g
                scope tab
                
                [ Q E ]
                [ T F ]
                """);

            var doc = DashSpecTestRowTypes.ParseDashboard("""
@tab demo
  report
  title = "demo"
  include layout "layouts/grid.dashlayout"
  card a as "A" ref Q
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card b as "B" ref E
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card c as "C" ref T
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card d as "D" ref F
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  end report
end tab
""", dir);

            Assert.NotNull(doc.Tabs[0].LayoutBoard);
            Assert.Equal(2, doc.Tabs[0].LayoutBoard!.RowCount);
            var layout = TabLayoutCompactor.Compact(doc, "demo");
            Assert.Equal(6, layout["a"].Span);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_include_layout_conflicts_with_inline_tab_layout()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "grid.dashlayout"), """
                @layout g
                scope tab
                [ Q ]
                """);

            var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
                @tab demo
                  !include "layouts/grid.dashlayout"
                  connect
                  layout board
                  [ Q ]
                  end layout board
                  end connect
                  report
                  title = "demo"
                  card a as "A" ref Q
                  diagram bar
                  x = a y
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end tab
                """, dir));

            Assert.Contains("layout board", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_filter_ref_and_toolbar_layout_board()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                  ref = D
                end show
              end filter
              filter app_name
                bind field
                  column = dbo.t.app
                end bind
                show
                  label = "App"
                  ref = A
                  widget = combobox
                end show
              end filter
              filter user_name
                bind field
                  column = dbo.t.user
                end bind
                show
                  label = "User"
                  ref = U
                  widget = combobox
                end show
              end filter
              toolbar
              [ D A ]
              [ U ]
              end toolbar
              card c as "C"
              bind
                usage_date
              end bind
              diagram number
              value = n
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal("D", doc.Filters[0].LayoutRef);
        Assert.NotNull(doc.ToolbarBoard);
        Assert.Equal(2, doc.ToolbarBoard!.RowCount);
        Assert.Equal(["usage_date", "app_name", "user_name"], doc.DashboardFilters);
    }

    [Fact]
    public void ToolbarLayoutCompactor_places_board_on_grid()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              layout grid
              columns = 12
              end grid
              defaults
                filter.d1.range = -7d..today
              end defaults
              filter d1
                bind date
                  column = c1
                end bind
                show
                  label = "D1"
                  ref = D
                end show
              end filter
              filter f1
                bind field
                  column = c2
                end bind
                show
                  label = "F1"
                  ref = A
                  widget = combobox
                end show
              end filter
              filter f2
                bind field
                  column = c3
                end bind
                show
                  label = "F2"
                  ref = U
                  widget = combobox
                end show
              end filter
              toolbar
              [ D A ]
              [ U ]
              end toolbar
              card c as "C"
              bind
                d1
              end bind
              diagram number
              value = n
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var layout = ToolbarLayoutCompactor.Compact(doc);

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["d1"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["f1"]);
        Assert.Equal(new PlacementDefinition(2, 1, 12), layout["f2"]);
    }

    [Fact]
    public void ToolbarLayoutCompactor_applies_weighted_board_row()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              layout grid
              columns = 12
              end grid
              defaults
                filter.d1.range = -7d..today
              end defaults
              filter d1
                bind date
                  column = c1
                end bind
                show
                  label = "D1"
                  ref = D
                end show
              end filter
              filter f1
                bind field
                  column = c2
                end bind
                show
                  label = "F1"
                  ref = P
                  widget = combobox
                end show
              end filter
              toolbar
              [ D:1 P:3 ]
              end toolbar
              card c as "C"
              bind
                d1
                f1
              end bind
              diagram number
              value = n
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var layout = ToolbarLayoutCompactor.Compact(doc);

        Assert.Equal(new PlacementDefinition(1, 1, 3), layout["d1"]);
        Assert.Equal(new PlacementDefinition(1, 4, 9), layout["f1"]);
    }

    [Fact]
    public void ToolbarLayoutCompactor_filter_place_overrides_board()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              layout grid
              columns = 12
              end grid
              defaults
                filter.d1.range = -7d..today
              end defaults
              filter d1
                bind date
                  column = c1
                end bind
                show
                  label = "D1"
                  ref = D
                end show
              end filter
              place { row = 2 col = 1 span = 6 }
              filter f1
                bind field
                  column = c2
                end bind
                show
                  label = "F1"
                  ref = P
                  widget = combobox
                end show
              end filter
              toolbar
              [ D P ]
              end toolbar
              card c as "C"
              bind
                d1
                f1
              end bind
              diagram number
              value = n
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var layout = ToolbarLayoutCompactor.Compact(doc);

        Assert.Equal(new PlacementDefinition(2, 1, 6), layout["d1"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["f1"]);
    }

    [Fact]
    public void Parse_include_toolbar_dashlayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-toolbar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "tb.dashlayout"), """
                @layout tb
                scope toolbar
                
                [ D A ]
                [ U ]
                """);
            File.WriteAllText(Path.Combine(dir, "root.dashspec"), """
                @dashboard t
                  !include "query-row-types.dashtype"
                  !include "layouts/tb.dashlayout"
                  report
                  title = "T"
                  defaults
                    filter.d1.range = -7d..today
                  end defaults
                  filter d1
                    bind date
                      column = c1
                    end bind
                    show
                      label = "D1"
                      ref = D
                    end show
                  end filter
                  filter f1
                    bind field
                      column = c2
                    end bind
                    show
                      label = "F1"
                      ref = A
                      widget = combobox
                    end show
                  end filter
                  filter f2
                    bind field
                      column = c3
                    end bind
                    show
                      label = "F2"
                      ref = U
                      widget = combobox
                    end show
                  end filter
                  card c as "C"
                  bind
                    d1
                  end bind
                  diagram number
                  value = n
                  end number
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end dashboard
                """);

            var doc = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(Path.Combine(dir, "root.dashspec")), dir);

            Assert.NotNull(doc.ToolbarBoard);
            Assert.Equal(["d1", "f1", "f2"], doc.DashboardFilters);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_toolbar_board_rejects_flat_list_combo()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.d1.range = -7d..today
              end defaults
              filter d1
                bind date
                  column = c1
                end bind
                show
                  label = "D1"
                  ref = D
                end show
              end filter
              toolbar d1
              toolbar
              [ D ]
              end toolbar
              card c as "C"
              bind
                d1
              end bind
              diagram number
              value = n
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));

        Assert.Contains("cannot combine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_layout_module_requires_scope()
    {
        var ex = Assert.Throws<DashSpecParseException>(() =>
            LayoutModuleParser.ParseLayoutFile("""
                @layout g
                
                [ Q ]
                """));

        Assert.Contains("requires scope", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_include_tab_layout_rejects_toolbar_scope()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "tb.dashlayout"), """
                @layout tb
                scope toolbar
                [ D ]
                """);

            var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
                @tab demo
                  !include "layouts/tb.dashlayout"
                  report
                  title = "demo"
                  card a as "A" ref D
                  diagram bar
                  x = a y
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end tab
                """, dir));

            Assert.Contains("scope toolbar", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("tab", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_include_toolbar_rejects_tab_scope()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-toolbar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "grid.dashlayout"), """
                @layout g
                scope tab
                [ Q ]
                """);

            var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
                @dashboard t
                  !include "query-row-types.dashtype"
                  !include "layouts/grid.dashlayout"
                  report
                  title = "T"
                  defaults
                    filter.d1.range = -7d..today
                  end defaults
                  filter d1
                    bind date
                      column = c1
                    end bind
                    show
                      label = "D1"
                      ref = Q
                    end show
                  end filter
                  card c as "C"
                  bind
                    d1
                  end bind
                  diagram number
                  value = n
                  end number
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end dashboard
                """, dir));

            Assert.Contains("scope tab", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("toolbar", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TabLayoutPlanner_places_group_inner_cards_and_top_level_rows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-layout-group-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "grouped.dashlayout"), """
                @layout grouped
                scope tab

                group distribution {
                  title = "Distribution"
                  [ Q E ]
                }
                [ T ]
                """);

            var doc = DashSpecTestRowTypes.ParseDashboard("""
                @tab demo
                  !include "layouts/grouped.dashlayout"
                  report
                  title = "demo"
                  card a as "A" ref Q
                  diagram bar
                  x = a y
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  card b as "B" ref E
                  diagram bar
                  x = a y
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  card c as "C" ref T
                  diagram bar
                  x = a y
                  end bar
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end tab
                """, dir);

            var context = TabLayoutCompactor.ResolveContext(doc, "demo");
            var plan = TabLayoutCompactor.TryBuildLayoutPlan(context);

            Assert.NotNull(plan);
            Assert.Equal(2, plan!.Entries.Count);
            Assert.True(plan.Groups.ContainsKey("distribution"));
            var group = plan.Groups["distribution"];
            Assert.Equal("Distribution", group.Title);
            Assert.Equal(1, group.OuterRow);
            Assert.Equal(new PlacementDefinition(1, 1, 6), group.InnerPlacements["a"]);
            Assert.Equal(new PlacementDefinition(1, 7, 6), group.InnerPlacements["b"]);
            Assert.Equal(new PlacementDefinition(2, 1, 12), plan.TopLevelPlacements["c"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TabLayoutPlanner_places_nest_in_bracket_cell_with_inner_rows()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
@tab demo
  connect
  layout board
  nest strip {
    [ E ]
    [ T ]
  }
  [ Q strip ]
  end layout board
  end connect
  report
  title = "demo"
  card wide as "Wide" ref Q
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card inner_a as "Inner A" ref E
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  card inner_b as "Inner B" ref T
  diagram bar
  x = a y
  end bar
  data flow { fixture_src [rows] -> [rows] __diagram__ }
  end card
  end report
end tab
""");

        var context = TabLayoutCompactor.ResolveContext(doc, "demo");
        var plan = TabLayoutCompactor.TryBuildLayoutPlan(context);

        Assert.NotNull(plan);
        Assert.True(plan!.Nests.ContainsKey("strip"));
        var nest = plan.Nests["strip"];
        Assert.Equal(new PlacementDefinition(1, 7, 6), nest.OuterPlacement);
        Assert.Equal(new PlacementDefinition(1, 1, 12), nest.InnerPlacements["inner_a"]);
        Assert.Equal(new PlacementDefinition(2, 1, 12), nest.InnerPlacements["inner_b"]);
        Assert.Equal(new PlacementDefinition(1, 1, 6), plan.TopLevelPlacements["wide"]);
    }
}