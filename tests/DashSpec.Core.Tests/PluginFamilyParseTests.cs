using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class PluginFamilyParseTests
{
    [Fact]
    public void Parse_module_extensions_use()
    {
        var text = """
            @tab soak
              runtime
              manifest = "demo.toml"
              end runtime
              extensions
              use card_export
              end extensions
              report
              title = "Soak"
              filter date usage_date on usage_date as "Date" default -7d..today
              toolbar usage_date
              card peak as "Peak"
              bind
                usage_date
              end bind
              diagram line
              x = usage_date y
              end line
              datasource view demo.v_peak
              end card
              end report
            end tab
            """;

        var document = DashSpecParser.Parse(text);
        Assert.NotNull(document.ModuleExtensions);
        Assert.Contains("card_export", document.ModuleExtensions!.EnabledPluginIds);
    }

    [Fact]
    public void Parse_card_extension_block_when_keyword_enabled()
    {
        var options = new DashSpecParseOptions
        {
            ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "buttons" },
        };

        var text = """
            @tab soak
              runtime
              manifest = "demo.toml"
              end runtime
              report
              title = "Soak"
              filter date usage_date on usage_date as "Date" default -7d..today
              toolbar usage_date
              card peak as "Peak"
              buttons
              export
              label = "Export"
              action = csv_export
              end export
              end buttons
              bind
                usage_date
              end bind
              diagram line
              x = usage_date y
              end line
              datasource view demo.v_peak
              end card
              end report
            end tab
            """;

        var document = DashSpecParser.Parse(text, specDirectory: null, options);
        var card = document.Cards.Single();
        Assert.Single(card.ExtensionBlocks);
        Assert.Equal("buttons", card.ExtensionBlocks[0].Keyword);
        Assert.Single(card.ExtensionBlocks[0].Nested);
        Assert.Equal("csv_export", card.ExtensionBlocks[0].Nested[0].Properties["action"]);
    }

    [Fact]
    public void Parse_card_views_extension_block_when_keyword_enabled()
    {
        var options = new DashSpecParseOptions
        {
            ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
        };

        var text = """
            @tab soak
              runtime
              manifest = "demo.toml"
              end runtime
              report
              title = "Soak"
              filter date usage_date on usage_date as "Date" default -7d..today
              toolbar usage_date
              card peak as "Peak"
              views
              default = heatmap
              line
              label = "Line"
              diagram = demo_peak_line
              end line
              heatmap
              label = "Heatmap"
              diagram = demo_peak_heatmap
              end heatmap
              end views
              bind
                usage_date
              end bind
              diagram demo_peak_heatmap
              datasource view demo.v_peak
              end card
              end report
            end tab
            """;

        var document = DashSpecParser.Parse(text, specDirectory: null, options);
        var card = document.Cards.Single();
        var views = Assert.Single(card.ExtensionBlocks);
        Assert.Equal("views", views.Keyword);
        Assert.Equal("heatmap", views.Properties["default"]);
        Assert.Equal(2, views.Nested.Count);
        Assert.Equal("demo_peak_line", views.Nested[0].Properties["diagram"]);
    }
}
