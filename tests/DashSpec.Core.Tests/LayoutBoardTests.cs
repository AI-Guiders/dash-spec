using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class LayoutBoardTests
{
    [Fact]
    public void Parse_card_ref_and_tab_layout_board()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo as "Demo" {
              layout {
                [ Q E ]
                [ T F ]
              }
            }

            card peak_by_app as "Peak" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card peak_apps as "Apps" ref E {
              diagram heatmap { x = a y = b value = c }
              datasource view dbo.t
            }
            card idle as "Idle" ref T {
              diagram heatmap { x = a y = b value = c }
              datasource view dbo.t
            }
            card utilization as "Util" ref F {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            """);

        Assert.Equal("Q", doc.Cards[0].LayoutRef);
        Assert.NotNull(doc.Tabs[0].LayoutBoard);
        Assert.Equal(2, doc.Tabs[0].LayoutBoard!.RowCount);
        Assert.Equal(2, doc.Tabs[0].LayoutBoard.ColumnCount);
    }

    [Fact]
    public void TabLayoutBoardResolver_places_2x2_grid()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo {
              layout {
                [ Q E ]
                [ T F ]
              }
            }

            card a as "A" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card b as "B" ref E {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card c as "C" ref T {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card d as "D" ref F {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
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
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo {
              layout {
                [ Q W ]
                [ E ]
              }
            }

            card a as "A" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card b as "B" ref W {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card c as "C" ref E {
              diagram heatmap { x = a y = b value = c }
              datasource view dbo.t
            }
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
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo {
              layout {
                [ Q E ]
                [ R T Y ]
                [ F ]
              }
            }

            card q as "Q" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card e as "E" ref E {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card r as "R" ref R {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card t as "T" ref T {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card y as "Y" ref Y {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card f as "F" ref F {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            """);

        Assert.Equal(3, doc.Tabs[0].LayoutBoard!.RowCount);
        Assert.Equal(3, doc.Tabs[0].LayoutBoard.ColumnCount);

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

                [ Q E ]
                [ T F ]
                """);

            var doc = DashSpecParser.Parse("""
                @tab demo

                include layout "layouts/grid.dashlayout"

                card a as "A" ref Q {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                card b as "B" ref E {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                card c as "C" ref T {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                card d as "D" ref F {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
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
                [ Q ]
                """);

            var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
                @tab demo

                include layout "layouts/grid.dashlayout"

                tab demo {
                  layout { [ Q ] }
                }

                card a as "A" ref Q {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                """, dir));

            Assert.Contains("twice", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_filter_ref_and_toolbar_layout_board()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Date" ref D default -7d..today
              filter field app_name on dbo.t.app as "App" ref A widget combobox
              filter field user_name on dbo.t.user as "User" ref U widget combobox
              toolbar {
                [ D A ]
                [ U ]
              }
              card c as "C" {
                bind usage_date
                diagram number { value = n }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal("D", doc.Filters[0].LayoutRef);
        Assert.NotNull(doc.ToolbarBoard);
        Assert.Equal(2, doc.ToolbarBoard!.RowCount);
        Assert.Equal(["usage_date", "app_name", "user_name"], doc.DashboardFilters);
    }

    [Fact]
    public void ToolbarLayoutCompactor_places_board_on_grid()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              layout grid { columns = 12 }
              filter date d1 on c1 as "D1" ref D default -7d..today
              filter field f1 on c2 as "F1" ref A widget combobox
              filter field f2 on c3 as "F2" ref U widget combobox
              toolbar {
                [ D A ]
                [ U ]
              }
              card c as "C" {
                bind d1
                diagram number { value = n }
                datasource view dbo.t
              }
            }
            """);

        var layout = ToolbarLayoutCompactor.Compact(doc);

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["d1"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["f1"]);
        Assert.Equal(new PlacementDefinition(2, 1, 12), layout["f2"]);
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

                [ D A ]
                [ U ]
                """);
            File.WriteAllText(Path.Combine(dir, "root.dashspec"), """
                @dashboard t
                dashboard "T" {
                  include toolbar "layouts/tb.dashlayout"
                  filter date d1 on c1 as "D1" ref D default -7d..today
                  filter field f1 on c2 as "F1" ref A widget combobox
                  filter field f2 on c3 as "F2" ref U widget combobox
                  card c as "C" {
                    bind d1
                    diagram number { value = n }
                    datasource view dbo.t
                  }
                }
                """);

            var doc = DashSpecParser.Parse(File.ReadAllText(Path.Combine(dir, "root.dashspec")), dir);

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
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date d1 on c1 as "D1" ref D default -7d..today
              toolbar { d1 }
              toolbar { [ D ] }
              card c as "C" {
                bind d1
                diagram number { value = n }
                datasource view dbo.t
              }
            }
            """));

        Assert.Contains("cannot combine a layout board with a flat filter list", ex.Message);
    }
}
