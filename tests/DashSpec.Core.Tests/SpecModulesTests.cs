using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class SpecModulesTests
{
    [Fact]
    public void ChartOrientationParser_accepts_aliases()
    {
        Assert.Equal(ChartOrientation.Horizontal, ChartOrientationParser.Parse("barh"));
        Assert.Equal(ChartOrientation.Vertical, ChartOrientationParser.Parse("vertical"));
        Assert.Equal(ChartOrientation.Vertical, ChartOrientationParser.Parse("unknown", ChartOrientation.Vertical));
    }

    [Fact]
    public void SpecLibrary_loads_presentation_and_transform_sections()
    {
        var library = SpecLibrary.Parse(
        [
            "# presets",
            "[presentation.line_bottom_300]",
            "legend = \"bottom\"",
            "height = \"300\"",
            "",
            "[transform.series.top5]",
            "max = 5",
            "other = \"Other\"",
        ]);

        Assert.Equal("bottom", library.TryGetPresentation("line_bottom_300")!["legend"]);
        Assert.Equal(5, library.TryGetSeriesTransform("top5")!.Max);
    }

    [Fact]
    public void SpecLibrary_loads_diagram_presets()
    {
        var library = SpecLibrary.Parse(
        [
            "[diagram.demo_peak_line]",
            "kind = \"line\"",
            "render = \"chartjs\"",
            "presentation = \"line_bottom_300\"",
            "\"transform.series\" = \"top5\"",
            "x = \"usage_date\"",
            "y = \"peak\"",
            "series = \"app_name\"",
        ]);

        var preset = library.TryGetDiagram("demo_peak_line");
        Assert.NotNull(preset);
        Assert.Equal("line", preset!.Kind);
        Assert.Equal("chartjs", preset.Render);
        Assert.Equal("line_bottom_300", preset.PresentationPreset);
        Assert.Equal("top5", preset.SeriesTransformPreset);
        Assert.Equal("usage_date", preset.Properties["x"]);
    }

    [Fact]
    public void SpecLibrary_loads_palette_sections()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var palette = library.TryGetPalette("brand");
        Assert.NotNull(palette);
        Assert.Equal("#111111,#222222", palette!["colors"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
    }

    [Fact]
    public void ChartColorResolver_maps_palette_and_series_overrides()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["color_palette"] = "brand",
                ["series_colors"] = "AVEVA:#2563eb",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            [],
            null,
            null,
            null,
            null);

        var series = new[]
        {
            new ChartSeries("Tekla", [1d]),
            new ChartSeries("AVEVA", [2d]),
            new ChartSeries("Other", [3d]),
            new ChartSeries("Unknown", [4d]),
        };

        var colored = ChartColorResolver.ApplySeriesColors(series, card, library);
        Assert.Equal("#e11d48", colored[0].Color);
        Assert.Equal("#2563eb", colored[1].Color);
        Assert.Equal("#999999", colored[2].Color);
        Assert.NotNull(colored[3].Color);

        var orderA = ChartColorResolver.ApplySeriesColors(
        [
            new ChartSeries("Beta", [1d]),
            new ChartSeries("Alpha", [2d]),
        ], card, library);
        var orderB = ChartColorResolver.ApplySeriesColors(
        [
            new ChartSeries("Alpha", [1d]),
            new ChartSeries("Beta", [2d]),
        ], card, library);
        Assert.Equal(
            orderA.First(x => x.Name == "Alpha").Color,
            orderB.First(x => x.Name == "Alpha").Color);
        Assert.Equal(
            orderA.First(x => x.Name == "Beta").Color,
            orderB.First(x => x.Name == "Beta").Color);
    }

    [Fact]
    public void ChartColorResolver_prefix_matches_palette_keys()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Cursor = \"#8b5cf6\"",
        ]);

        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["color_palette"] = "brand",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var colored = ChartColorResolver.ApplySeriesColors(
            [new ChartSeries("Cursor IDE", [1d])],
            card,
            library);
        Assert.Equal("#8b5cf6", colored[0].Color);
    }

    [Fact]
    public void ChartColorResolver_applies_dashboard_palette_when_diagram_has_none()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["series"] = "app_name",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var series = new[] { new ChartSeries("Tekla", [1d]) };
        var colored = ChartColorResolver.ApplySeriesColors(series, card, library, dashboardColorPalette: "brand");
        Assert.Equal("#e11d48", colored[0].Color);
    }

    [Fact]
    public void SpecResolveExporter_includes_dashboard_palette_and_effective_card()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.lus]",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
            "[diagram.d1]",
            "kind = \"line\"",
            "x = \"usage_date\"",
            "y = \"peak\"",
            "[card.c1]",
            "diagram = \"d1\"",
            "datasource = \"dbo.t\"",
        ]);

        var document = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              palette lus
              card c as "C"
              use c1
              end card
              end report
            end dashboard
""");

        var export = SpecResolveExporter.Export(document, library);
        Assert.Equal("lus", export.ColorPalette);
        Assert.Single(export.Cards);
        Assert.Equal("line", export.Cards[0].DiagramKind);
        Assert.Equal("lus", export.Cards[0].EffectiveColorPalette);
        Assert.Equal("usage_date", export.Cards[0].Diagram["x"]);
    }

    [Fact]
    public void Parse_dashboard_palette_directive()
    {
        var document = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              palette = "lus_apps"
              card c as "C"
              diagram number
              value = x
              end number
              datasource view dbo.t
              end card
              end report
            end dashboard
""");

        Assert.Equal("lus_apps", document.ColorPalette);
    }

    [Fact]
    public void Parse_palette_file_directive()
    {
        var document = DashSpecParser.Parse("""

            @dashboard t
              configuration
              palette = "palettes/brand.dashpalette"
              end configuration
              report
              title = "T"
              palette brand
              card c as "C"
              diagram number
              value = x
              end number
              datasource view dbo.t
              end card
              end report
            end dashboard
""");

        Assert.Equal("palettes/brand.dashpalette", document.PalettePath);
        Assert.Equal("brand", document.ColorPalette);
    }

    [Fact]
    public void PaletteModuleParser_loads_quoted_series_keys()
    {
        var library = PaletteModuleParser.LoadPaletteFile(WriteTempPalette("""
            @palette lus_apps
            
            palette
              colors = "#111111,#222222"
              default = "#999999"
              Tekla = "#e11d48"
              "Cursor IDE" = "#8b5cf6"
            end palette
            """));

        var palette = library.TryGetPalette("lus_apps");
        Assert.NotNull(palette);
        Assert.Equal("#111111,#222222", palette!["colors"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
        Assert.Equal("#8b5cf6", palette["Cursor IDE"]);
    }

    [Fact]
    public void PaletteModuleParser_resolves_const_refs_css_names_and_color_list()
    {
        var library = PaletteModuleParser.LoadPaletteFile(WriteTempPalette("""
            @palette brand
            
            const default = "#999999"
            const tekla = "#e11d48"
            const accent = blue
            
            palette
              default = default
              Tekla = tekla
              Other = default
              colors = [tekla, accent, green, orange]
            end palette
            """));

        var palette = library.TryGetPalette("brand");
        Assert.NotNull(palette);
        Assert.Equal("#999999", palette!["default"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
        Assert.Equal("#e11d48,#0000ff,#008000,#ffa500", palette["colors"]);
    }

    [Fact]
    public void PaletteModuleParser_keeps_legacy_colors_string()
    {
        var library = PaletteModuleParser.LoadPaletteFile(WriteTempPalette("""
            @palette legacy
            palette
              colors = "#111111,#222222"
              default = "#999999"
            end palette
            """));

        Assert.Equal("#111111,#222222", library.TryGetPalette("legacy")!["colors"]);
    }

    [Fact]
    public void SpecLibraryComposer_merges_palette_with_diagram_library()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-palette-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var specPath = Path.Combine(dir, "t.dashspec");
        File.WriteAllText(specPath, "@dashboard t\n  report\n  title = \"T\"\n  end report\nend dashboard");
        File.WriteAllText(Path.Combine(dir, "lib.toml"), "[diagram.d1]\nkind = \"line\"\n");
        File.WriteAllText(Path.Combine(dir, "brand.dashpalette"), """
            @palette brand
            palette
              default = "#999999"
            end palette
            """);

        var library = SpecLibraryComposer.Load(specPath, "lib.toml", "brand.dashpalette");
        Assert.NotNull(library);
        Assert.NotNull(library!.TryGetDiagram("d1"));
        Assert.Equal("#999999", library.TryGetPalette("brand")!["default"]);
    }

    [Fact]
    public void Parse_lus_palette_specs_when_repo_present()
    {
        var dir = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec";
        var soakPath = Path.Combine(dir, "lus-dev-soak.dashspec");
        if (!File.Exists(soakPath))
        {
            return;
        }

        var parseOptions = CreateExtendedPluginParseOptions();
        try
        {
            var soak = DashSpecParser.Parse(File.ReadAllText(soakPath), dir, parseOptions);
            Assert.Equal("palettes/lus-apps.dashpalette", soak.PalettePath);
            Assert.Equal("lus_apps", soak.ColorPalette);

            var library = SpecLibraryComposer.Load(soakPath, soak.DiagramLibraryPath, soak.PalettePath, dir, soak);
            Assert.Equal("#e11d48", library!.TryGetPalette("lus_apps")!["Tekla"]);

            var overviewPath = Path.Combine(dir, "lus-dev-overview.dashspec");
            if (File.Exists(overviewPath))
            {
                var overview = DashSpecParser.Parse(File.ReadAllText(overviewPath), dir, parseOptions);
                var overviewLibrary = SpecLibraryComposer.Load(
                    overviewPath,
                    overview.DiagramLibraryPath,
                    overview.PalettePath,
                    dir,
                    overview);
                Assert.NotNull(overviewLibrary!.TryGetDiagram("lus_peak_concurrent_heatmap"));

                var card = overview.Cards.First(c => c.Id == "peak_concurrent_proxy");
                var switched = CardViewSwitchApplier.Apply(card, "heatmap");
                var resolved = CardDiagramResolver.Resolve(switched, overviewLibrary);
                Assert.Equal("heatmap", resolved.Card.Diagram.Kind);
                Assert.Equal("matrix-canvas", resolved.RenderPluginId);
            }

            var stakePath = Path.Combine(dir, "lus-dev-stakeholder.dashspec");
            var stake = DashSpecParser.Parse(File.ReadAllText(stakePath), dir, parseOptions);
            Assert.Equal("lus_apps", stake.ColorPalette);
        }
        catch (DashSpecParseException ex) when (ex.Message.Contains("ADR-0029", StringComparison.OrdinalIgnoreCase))
        {
            // LUS specs not migrated to @tooltip + inspect yet (parent task).
        }
    }

    private static DashSpecParseOptions CreateExtendedPluginParseOptions() =>
        new()
        {
            ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "buttons",
                "views",
            },
            ExtensionBlockPluginIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["views"] = "card_views",
            },
            KnownActionHandlers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "csv_export",
                "switch_view",
            },
        };

    private static string WriteTempPalette(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "dashpalette-" + Guid.NewGuid().ToString("N") + ".dashpalette");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Parse_card_include_diagram_file_and_stdlib_presentation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-include-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        var stdlib = Path.Combine(dir, "stdlib", "presentation");
        Directory.CreateDirectory(stdlib);
        try
        {
            File.WriteAllText(Path.Combine(stdlib, "heatmap_tall.dashpresentation"), """
                @presentation heatmap_tall
                
                height = 420
                """);

            File.WriteAllText(Path.Combine(dir, "diagrams", "activity.dashdiagram"), """
                @diagram activity
                
                include presentation "<presentation/heatmap_tall>"
                
                heatmap
                  x = bucket_start_utc
                  y = app_name
                  value = event_count
                  x_format = time.short
                  x_step = 1h
                end heatmap
                """);

            SpecIncludeResolver.SetStdlibRootForTests(Path.Combine(dir, "stdlib"));

            var doc = DashSpecParser.Parse("""
                @dashboard t
                  !include "diagrams/activity.dashdiagram"
                  report
                  title = "T"
                  card c as "C"
                  diagram activity
                  datasource view dbo.t
                  end card
                  end report
                end dashboard
                """, dir);

            var card = doc.Cards.Single();
            Assert.Equal("heatmap", card.Diagram.Kind);
            Assert.Equal("bucket_start_utc", card.Diagram.Properties["x"]);
            Assert.Equal("420", card.Presentation!.Properties["height"]);
        }
        finally
        {
            SpecIncludeResolver.SetStdlibRootForTests(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_diagram_chrome_use_registers_module_presentation_presets()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "presentations"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "presentations", "bar-tall.dashpresentation"), """
                @presentation bar_tall

                legend = bottom
                height = 320
                y_max = 100
                """);

            File.WriteAllText(Path.Combine(dir, "diagrams", "util.dashdiagram"), """
                @diagram util

                chrome
                  use bar_tall
                  scale_value = percent
                end chrome

                bar
                  category = app_name
                  value = utilization_pct
                end bar
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t
                  !include "presentations/bar-tall.dashpresentation"
                  !include "diagrams/util.dashdiagram"
                  report
                  title = "T"
                  card c as "C"
                  diagram util
                  datasource view dbo.t
                  end card
                  end report
                end dashboard
                """, dir);

            Assert.Single(doc.ResolvedChartChromePresets);
            Assert.True(doc.ResolvedChartChromePresets.ContainsKey("bar_tall"));

            var library = SpecLibraryComposer.Load("spec.dashspec", null, null, dir, doc);
            var card = doc.Cards.Single();
            var resolved = CardDiagramResolver.Resolve(card, library).Card;
            var presentation = CardChromeResolver.ResolveChartPresentation(resolved, library);

            Assert.Equal(320, presentation.HeightPx);
            Assert.Equal(100, presentation.ValueAxisMax);
            Assert.Equal(ChartAxisScale.Percent, presentation.ValueAxisScale);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_treats_unknown_diagram_ident_as_library_preset()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card a as "A"
              diagram demo_custom_heatmap
              x = a
              y = b
              value = c
              end diagram
              datasource view dbo.t
              end card
              end report
            end dashboard
""").Cards[0];

        Assert.Equal("demo_custom_heatmap", card.Diagram.UsePreset);
        Assert.Equal("a", card.Diagram.Properties["x"]);
        Assert.Equal("c", card.Diagram.Properties["value"]);
    }

    [Fact]
    public void CardDiagramResolver_throws_when_preset_missing()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card a as "A"
              diagram missing_preset
              datasource view dbo.t
              end card
              end report
            end dashboard
""").Cards[0];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CardDiagramResolver.Resolve(card, library: null));
        Assert.Contains("missing_preset", ex.Message);
    }



    [Fact]
    public void Parse_diagram_library_preset_reference()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card c as "C"
              diagram demo_peak_line
              datasource view dbo.t
              end card
              end report
            end dashboard
""");

        var card = doc.Cards[0];
        Assert.Equal("demo_peak_line", card.Diagram.UsePreset);
        Assert.Empty(card.Diagram.Kind);
        Assert.Empty(card.Diagram.Properties);
    }

    [Fact]
    public void CardDiagramResolver_merges_diagram_preset()
    {
        var library = SpecLibrary.Parse(
        [
            "[presentation.line_bottom_300]",
            "legend = \"bottom\"",
            "height = \"300\"",
            "[transform.series.top5]",
            "max = 5",
            "other = \"Other\"",
            "[diagram.demo_peak_line]",
            "kind = \"line\"",
            "presentation = \"line_bottom_300\"",
            "\"transform.series\" = \"top5\"",
            "x = \"usage_date\"",
            "y = \"peak\"",
            "series = \"app_name\"",
        ]);

        var card = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card c as "C"
              diagram demo_peak_line
              datasource view dbo.t
              end card
              end report
            end dashboard
""").Cards[0];

        var resolved = CardDiagramResolver.Resolve(card, library);
        Assert.Equal("line", resolved.Card.Diagram.Kind);
        Assert.Equal("usage_date", resolved.Card.Diagram.Properties["x"]);
        Assert.Equal("line_bottom_300", resolved.Card.Presentation!.UsePreset);
        Assert.Equal("top5", resolved.Card.SeriesTransform!.UsePreset);

        var presentation = CardChromeResolver.ResolveChartPresentation(resolved.Card, library);
        Assert.Equal(300, presentation.HeightPx);
        Assert.Equal(5, CardChromeResolver.ResolveSeriesTransform(resolved.Card, library)!.Max);
    }
}
