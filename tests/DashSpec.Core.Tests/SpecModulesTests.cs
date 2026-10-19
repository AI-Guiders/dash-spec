using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Resolution;
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
        var library = SpecLibraryLoader.Parse(
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
        var library = SpecLibraryLoader.Parse(
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
        var library = SpecLibraryLoader.Parse(
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
        var library = SpecLibraryLoader.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var card = CardTestData.Create(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["color_palette"] = "brand",
                ["series_colors"] = "AVEVA:#2563eb",
            }, null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
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
        var library = SpecLibraryLoader.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Cursor = \"#8b5cf6\"",
        ]);

        var card = CardTestData.Create(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["color_palette"] = "brand",
            }, null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
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
        var library = SpecLibraryLoader.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var card = CardTestData.Create(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["series"] = "app_name",
            }, null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var series = new[] { new ChartSeries("Tekla", [1d]) };
        var colored = ChartColorResolver.ApplySeriesColors(series, card, library, dashboardColorPalette: "brand");
        Assert.Equal("#e11d48", colored[0].Color);
    }

    [Fact]
    public void SpecResolveExporter_includes_dashboard_palette_and_effective_card()
    {
        var library = SpecLibraryLoader.Parse(
        [
            "[palette.demo_apps]",
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

        var document = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              palette demo_apps
              card c as "C"
              use c1
              end card
              end report
            end dashboard
""");

        var export = SpecResolveExporter.Export(document, library);
        Assert.Equal("demo_apps", export.ColorPalette);
        Assert.Single(export.Cards);
        Assert.Equal("line", export.Cards[0].DiagramKind);
        Assert.Equal("demo_apps", export.Cards[0].EffectiveColorPalette);
        Assert.Equal("usage_date", export.Cards[0].Diagram["x"]);
    }

    [Fact]
    public void Parse_dashboard_palette_directive()
    {
        var document = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              palette = "demo_apps"
              card c as "C"
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal("demo_apps", document.ColorPalette);
    }

    [Fact]
    public void Parse_imported_palette_bound_with_connect_use()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-palette-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "brand.ds"), """
                module palette brand
                namespace Brand

                palette
                  default = "#999999"
                end palette
                """);

            var document = DashSpecTestRowTypes.ParseDashboard("""
                module dashboard t
                      import types from Fixtures.QueryRowTypes
                      import palettes from Brand
                  connect
                    use palette brand
                  end connect
                  report
                  title = "T"
                  card c as "C"
                  diagram number
                  value = x
                  end number
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end dashboard
                """, dir);

            Assert.NotNull(document.Palette);
            Assert.Equal("brand", document.Palette!.Value.Id);
            Assert.Equal("brand", document.ColorPalette);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PaletteModuleParser_loads_quoted_series_keys()
    {
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette demo_apps
            
            palette
              colors = "#111111,#222222"
              default = "#999999"
              Tekla = "#e11d48"
              "Cursor IDE" = "#8b5cf6"
            end palette
            """));

        var palette = library.TryGetPalette("demo_apps");
        Assert.NotNull(palette);
        Assert.Equal("#111111,#222222", palette!["colors"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
        Assert.Equal("#8b5cf6", palette["Cursor IDE"]);
    }

    [Fact]
    public void PaletteModuleParser_resolves_const_refs_css_names_and_color_list()
    {
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette brand
            
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
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette legacy
            palette
              colors = "#111111,#222222"
              default = "#999999"
            end palette
            """));

        Assert.Equal("#111111,#222222", library.TryGetPalette("legacy")!["colors"]);
    }

    [Fact]
    public void PaletteModuleParser_parses_bare_hex_in_colors_list()
    {
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette brand
            
            palette
              default = "#999999"
              colors = [#e11d48, #2563eb, #fff]
            end palette
            """));

        Assert.Equal("#999999", library.TryGetPalette("brand")!["default"]);
        Assert.Equal("#e11d48,#2563eb,#ffffff", library.TryGetPalette("brand")!["colors"]);
    }

    [Fact]
    public void PaletteModuleParser_accepts_color_call_syntax()
    {
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette brand
            
            const tekla = "#e11d48"
            
            palette
              Tekla = color(tekla)
              colors = [color(#0000ff), green]
            end palette
            """));

        var palette = library.TryGetPalette("brand");
        Assert.NotNull(palette);
        Assert.Equal("#e11d48", palette!["Tekla"]);
        Assert.Equal("#0000ff,#008000", palette["colors"]);
    }

    [Fact]
    public void PaletteModuleParser_treats_line_start_slash_slash_as_comment()
    {
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette brand
            
            // not a color — line comment
            palette
              default = "#999999"
              colors = [#e11d48]
            end palette
            """));

        Assert.Equal("#e11d48", library.TryGetPalette("brand")!["colors"]);
    }

    [Fact]
    public void PaletteModuleParser_line_start_hash_hex_is_color_not_comment()
    {
        var library = LoadPaletteFromFile(WriteTempPalette("""
            module palette brand
            palette
              accent = #e11d48
              colors = [#2563eb]
            end palette
            """));

        Assert.Equal("#e11d48", library.TryGetPalette("brand")!["accent"]);
        Assert.Equal("#2563eb", library.TryGetPalette("brand")!["colors"]);
    }

    [Fact]
    public void SpecLibraryComposer_merges_palette_with_diagram_library()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-palette-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "brand.ds"), """
            module palette brand
                namespace Brand

                palette
                  default = "#999999"
                end palette
            """);
        File.WriteAllText(Path.Combine(dir, "lib.toml"), "[diagram.d1]\nkind = \"line\"\n");
        var specPath = Path.Combine(dir, "t.ds");
        var specText = """
            module dashboard t
                namespace T
                import palettes from Brand

            connect
                use palette brand
            end connect

            report
                title = "T"
            end report
            end dashboard
            """;
        File.WriteAllText(specPath, specText);

        var document = DashSpecTestRowTypes.ParseDashboard(specText, dir);
        var library = SpecLibraryComposer.Load(specPath, "lib.toml", dir, document);
        Assert.NotNull(library);
        Assert.NotNull(library!.TryGetDiagram("d1"));
        Assert.Equal("#999999", library.TryGetPalette("brand")!["default"]);
    }

    [Fact]
    public void Parse_demo_palette_specs()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo"));
        var soakPath = Path.Combine(dir, "demo-soak.ds");

        var parseOptions = CreateExtendedPluginParseOptions();

        var soak = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(soakPath), dir, parseOptions);
        Assert.NotNull(soak.Palette);
        Assert.Equal("demo_apps", soak.Palette!.Value.Id);
        Assert.Equal("demo_apps", soak.ColorPalette);

        var library = SpecLibraryComposer.Load(soakPath, soak.DiagramLibraryPath?.Value, dir, soak);
        Assert.Equal("#94a3b8", library!.TryGetPalette("demo_apps")!["default"]);

        // demo-analytics.ds is a tab unit (ADR-0101): its cards bind filters owned by the dashboard,
        // so it is parsed through demo-soak.ds (import tabs from Demo.Analytics), not standalone.
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

    /// <summary>Parses a palette unit file into a library: the product resolves palettes in the parser, so the composition lives here.</summary>
    private static SpecLibrary LoadPaletteFromFile(string path)
    {
        var (id, properties) = PaletteModuleParser.ParsePaletteFile(File.ReadAllText(path));
        return SpecLibrary.FromPalette(id, properties);
    }

    private static string WriteTempPalette(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "dashpalette-" + Guid.NewGuid().ToString("N") + ".ds");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Parse_card_imports_diagram_file_with_stdlib_presentation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-include-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "diagrams", "activity.ds"), """
                module diagram activity
                namespace Diagrams

                presentation
                  use heatmap_tall
                end presentation

                heatmap
                  x = bucket_start_utc
                  y = app_name
                  value = event_count
                  x_format = time.short
                  x_step = 1h
                end heatmap
                """);

            var doc = DashSpecTestRowTypes.ParseDashboard("""
                module dashboard t
                  import types from Fixtures.QueryRowTypes
                  import diagrams from Diagrams
                  import presentations from DashSpec.Std
                  report
                  title = "T"
                  card c as "C"
                  diagram activity
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end dashboard
                """, dir);

            var card = doc.Cards.Single();
            Assert.Equal("heatmap", card.Diagram.Kind);
            Assert.Equal("bucket_start_utc", card.Diagram.Properties["x"]);
            Assert.Equal("heatmap_tall", card.Presentation!.Value.UsePreset?.Value);
            Assert.Equal("420", doc.ResolvedChartChromePresets["heatmap_tall"].Properties["height"]);
        }
        finally
        {
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
            File.WriteAllText(Path.Combine(dir, "presentations", "bar-tall.ds"), """
                module presentation bar_tall
                namespace Presentations

                legend = bottom
                height = 320
                y_max = 100
                """);

            File.WriteAllText(Path.Combine(dir, "diagrams", "util.ds"), """
                module diagram util
                namespace Diagrams

                chrome
                  use bar_tall
                  scale_value = percent
                end chrome

                bar
                  category = app_name
                  value = utilization_pct
                end bar
                """);

            var doc = DashSpecTestRowTypes.ParseDashboard("""
                module dashboard t
                  import types from Fixtures.QueryRowTypes
                  import diagrams from Diagrams
                  import presentations from Presentations
                  report
                  title = "T"
                  card c as "C"
                  diagram util
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end dashboard
                """, dir);

            Assert.Single(doc.ResolvedChartChromePresets);
            Assert.True(doc.ResolvedChartChromePresets.ContainsKey("bar_tall"));

            var library = SpecLibraryComposer.Load("spec.dashspec", null, dir, doc);
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
        var card = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card a as "A"
              diagram demo_custom_heatmap
              x = a
              y = b
              value = c
              end diagram
              data flow { fixture_src [rows] -> [rows] __diagram__ }
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
        var card = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card a as "A"
              diagram missing_preset
              data flow { fixture_src [rows] -> [rows] __diagram__ }
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
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card c as "C"
              diagram demo_peak_line
              data flow { fixture_src [rows] -> [rows] __diagram__ }
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
        var library = SpecLibraryLoader.Parse(
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

        var card = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card c as "C"
              diagram demo_peak_line
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""").Cards[0];

        var resolved = CardDiagramResolver.Resolve(card, library);
        Assert.Equal("line", resolved.Card.Diagram.Kind);
        Assert.Equal("usage_date", resolved.Card.Diagram.Properties["x"]);
        Assert.Equal("line_bottom_300", resolved.Card.Presentation!.Value.UsePreset?.Value);
        Assert.Equal("top5", resolved.Card.SeriesTransform!.Value.UsePreset?.Value);

        var presentation = CardChromeResolver.ResolveChartPresentation(resolved.Card, library);
        Assert.Equal(300, presentation.HeightPx);
        Assert.Equal(5, CardChromeResolver.ResolveSeriesTransform(resolved.Card, library)!.Max);
    }
}
