using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class ChartDataBuilderTests
{
    [Fact]
    public void BuildHeatmap_pivots_rows_and_sorts_axes()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "usage_date",
            ["y"] = "user_name",
            ["value"] = "peak_concurrent_apps",
            ["tooltip"] = "peak_apps",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 23),
                ["user_name"] = "alice",
                ["peak_concurrent_apps"] = 3,
                ["peak_apps"] = "Chrome",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 25),
                ["user_name"] = "alice",
                ["peak_concurrent_apps"] = 6,
                ["peak_apps"] = "AutoCAD, Revit, Chrome",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 25),
                ["user_name"] = "bob",
                ["peak_concurrent_apps"] = 10,
                ["peak_apps"] = "Tekla, AutoCAD",
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Equal(["2026-06-23", "2026-06-25"], matrix.XLabels);
        Assert.Equal(["bob", "alice"], matrix.YLabels);
        Assert.Equal(10, matrix.Cells[0][1]);
        Assert.Equal(6, matrix.Cells[1][1]);
        Assert.Equal(3, matrix.Cells[1][0]);
        Assert.Null(matrix.Cells[0][0]);
        Assert.Equal(3, matrix.Min);
        Assert.Equal(10, matrix.Max);
        Assert.Equal("AutoCAD, Revit, Chrome", matrix.Tooltips![1][1]);
    }

    [Fact]
    public void BuildHeatmap_merges_y_labels_by_y_format()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "usage_date",
            ["y"] = "user_name",
            ["value"] = "peak_concurrent_apps",
            ["tooltip"] = "peak_apps",
            ["y_format"] = "user.short",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 24),
                ["user_name"] = @"CORP\LonelySoul",
                ["peak_concurrent_apps"] = 10,
                ["peak_apps"] = "AutoCAD, Revit",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 24),
                ["user_name"] = "LonelySoul",
                ["peak_concurrent_apps"] = 10,
                ["peak_apps"] = "Tekla",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 23),
                ["user_name"] = "LonelySoul",
                ["peak_concurrent_apps"] = 3,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 25),
                ["user_name"] = "LonelySoul",
                ["peak_concurrent_apps"] = 6,
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Single(matrix.YLabels);
        Assert.Equal("LonelySoul", matrix.YLabels[0]);
        Assert.Equal(3, matrix.Cells[0][0]);
        Assert.Equal(10, matrix.Cells[0][1]);
        Assert.Equal(6, matrix.Cells[0][2]);
        Assert.Equal("AutoCAD, Revit, Tekla", matrix.Tooltips![0][1]);
    }

    [Fact]
    public void BuildHeatmap_formats_x_axis_with_time_short()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket_start_utc",
            ["y"] = "app_name",
            ["value"] = "event_count",
            ["x_format"] = "time.short",
            ["y_format"] = "raw",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 14, 0, 0),
                ["app_name"] = "Cursor IDE",
                ["event_count"] = 120d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 8, 0, 0),
                ["app_name"] = "Cursor IDE",
                ["event_count"] = 40d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 14, 0, 0),
                ["app_name"] = "Google Chrome",
                ["event_count"] = 15d,
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Equal(["08:00", "14:00"], matrix.XLabels);
        Assert.Equal(["Cursor IDE", "Google Chrome"], matrix.YLabels);
        Assert.Equal(40, matrix.Cells[0][0]);
        Assert.Equal(120, matrix.Cells[0][1]);
        Assert.Equal(15, matrix.Cells[1][1]);
    }

    [Fact]
    public void BuildHeatmap_with_x_step_fills_full_day_hour_grid()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket_start_utc",
            ["y"] = "app_name",
            ["value"] = "event_count",
            ["x_format"] = "time.short",
            ["x_step"] = "1h",
            ["y_format"] = "raw",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 0, 0, 0),
                ["app_name"] = "Cursor IDE",
                ["event_count"] = 81d,
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Equal(24, matrix.XLabels.Count);
        Assert.Equal("00:00", matrix.XLabels[0]);
        Assert.Equal("23:00", matrix.XLabels[23]);
        Assert.Equal(81, matrix.Cells[0][0]);
        Assert.Null(matrix.Cells[0][1]);
    }

    [Fact]
    public void BuildLineOrBar_fills_five_minute_grid_when_x_step_set()
    {
        var diagram = new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket",
            ["y"] = "n",
            ["x_step"] = "5m",
            ["x_format"] = "time.short",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 7, 40, 0),
                ["n"] = 1d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 8, 5, 0),
                ["n"] = 2d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);
        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(6, payload.Labels.Count);
        Assert.Equal("07:40", payload.Labels[0]);
        Assert.Equal("07:45", payload.Labels[1]);
        Assert.Equal("08:00", payload.Labels[4]);
        Assert.Equal("08:05", payload.Labels[5]);
        Assert.Equal(1d, payload.Series[0].Values[0]);
        Assert.Null(payload.Series[0].Values[1]);
        Assert.Equal(2d, payload.Series[0].Values[5]);
    }

    [Fact]
    public void BuildLineOrBar_honors_arbitrary_x_step_from_spec()
    {
        var diagram = new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket",
            ["y"] = "n",
            ["x_step"] = "10m",
            ["x_format"] = "time.short",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 7, 0, 0),
                ["n"] = 1d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 7, 25, 0),
                ["n"] = 2d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["07:00", "07:10", "07:20"], payload.Labels);
        Assert.Equal(2d, payload.Series[0].Values[2]);
    }

    [Fact]
    public void BuildLineOrBar_builds_category_bar_for_app_name_axis()
    {
        var diagram = new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "app_name",
            ["y"] = "peak_concurrent_proxy",
            ["orientation"] = "horizontal",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "Tekla Structures",
                ["peak_concurrent_proxy"] = 12d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "AutoCAD",
                ["peak_concurrent_proxy"] = 8d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["Tekla Structures", "AutoCAD"], payload.Labels);
        Assert.Equal(12d, payload.Series[0].Values[0]);
        Assert.Equal(8d, payload.Series[0].Values[1]);
        Assert.NotNull(payload.Series[0].PointColors);
        Assert.Equal(2, payload.Series[0].PointColors!.Count);
    }

    [Fact]
    public void BuildLineOrBar_builds_donut_from_category_value()
    {
        var diagram = new DiagramDefinition("donut", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "location",
            ["value"] = "launch_count",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = "/PROJECTHUB",
                ["launch_count"] = 605d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = "/MOC",
                ["launch_count"] = 177d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["/PROJECTHUB", "/MOC"], payload.Labels);
        Assert.Equal(605d, payload.Series[0].Values[0]);
        Assert.Equal(177d, payload.Series[0].Values[1]);
        Assert.NotNull(payload.Series[0].PointColors);
    }

    [Fact]
    public void BuildLineOrBar_keeps_max_on_duplicate_categories()
    {
        var diagram = new DiagramDefinition("donut", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "location",
            ["value"] = "launch_count",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = "/MOC",
                ["launch_count"] = 100d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = "/MOC",
                ["launch_count"] = 77d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = "/SPOC-K",
                ["launch_count"] = 10d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["/MOC", "/SPOC-K"], payload.Labels);
        Assert.Equal(100d, payload.Series[0].Values[0]);
        Assert.Equal(10d, payload.Series[0].Values[1]);
    }

    [Fact]
    public void BuildLineOrBar_folds_extra_donut_categories_into_other()
    {
        var diagram = new DiagramDefinition("pie", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "name",
            ["value"] = "n",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "a", ["n"] = 50d },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "b", ["n"] = 40d },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "c", ["n"] = 30d },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "d", ["n"] = 10d },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var transform = new SeriesTransformSettings(Max: 3, OtherLabel: "Other");
        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, transform, card, null);

        Assert.Equal(["a", "b", "Other"], payload.Labels);
        Assert.Equal(50d, payload.Series[0].Values[0]);
        Assert.Equal(40d, payload.Series[0].Values[1]);
        Assert.Equal(40d, payload.Series[0].Values[2]);
    }

    [Fact]
    public void BuildChart_histogram_bins_numeric_values()
    {
        var diagram = new DiagramDefinition("histogram", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = "idle_minutes",
            ["bins"] = "4",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["idle_minutes"] = 1d },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["idle_minutes"] = 2d },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["idle_minutes"] = 8d },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["idle_minutes"] = 9d },
        ];

        var card = new CardDefinition(
            "h",
            "H",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildChart(rows, diagram, null, card, null);

        Assert.Equal(4, payload.Labels.Count);
        Assert.Equal(4, payload.Series[0].Values.Count);
        Assert.Equal(4d, payload.Series[0].Values.Sum(v => v ?? 0));
    }

    [Fact]
    public void BuildChart_scatter_emits_xy_points()
    {
        var diagram = new DiagramDefinition("scatter", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "idle_minutes",
            ["y"] = "peak_apps",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["idle_minutes"] = 12d,
                ["peak_apps"] = 3d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["idle_minutes"] = 40d,
                ["peak_apps"] = 7d,
            },
        ];

        var card = new CardDefinition(
            "s",
            "S",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildChart(rows, diagram, null, card, null);

        Assert.NotNull(payload.Points);
        Assert.Equal(2, payload.Points!.Count);
        Assert.Equal(12d, payload.Points[0].X);
        Assert.Equal(3d, payload.Points[0].Y);
        Assert.Equal(40d, payload.Points[1].X);
        Assert.Equal(7d, payload.Points[1].Y);
    }

    [Fact]
    public void ChartPresentation_area_and_sparkline_defaults()
    {
        var area = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fill"] = "area",
            },
            diagramKind: "line");
        Assert.True(area.FillArea);

        var areaKind = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            diagramKind: "area");
        Assert.True(areaKind.FillArea);

        var spark = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            diagramKind: "sparkline");
        Assert.True(spark.Sparkline);
        Assert.Equal("hidden", spark.Legend);
        Assert.Equal(64, spark.HeightPx);
    }
}
