using DashSpec.Core.Parsing;
using DashSpec.Execution.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class DocumentModuleParserTests
{
    [Fact]
    public void Parse_block_tab_module_with_standalone_filters_and_diagram_ref()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-block-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "diagrams", "x.ds"), """
                module diagram x
                    namespace Test.Diagrams
                
                number
                  value = n
                end number
                """);

            File.WriteAllText(Path.Combine(dir, "demo-apps.ds"), """
                module palette demo_apps
                    namespace Test.Palettes

                palette
                  default = "#94a3b8"
                end palette
                """);

            const string text = """
                module tab t
                  import diagrams from Test.Diagrams
                  import palettes from Test.Palettes
                  runtime
                  manifest = "cfg.toml"
                  end runtime
                  configuration
                  sqldialect = tsql
                  end configuration
                  connect
                  use palette demo_apps
                  end connect
                  report
                  title = "Tab title"
                  standalone
                  filter app
                    bind field
                      column = dbo.apps.name
                    end bind
                    show
                      label = "App"
                    end show
                  end filter
                  toolbar app
                  end standalone
                  card c as "C"
                  diagram x
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  bind
                    app
                  end bind
                  end card
                  end report
                end tab
                """;

            var doc = DashSpecTestRowTypes.ParseDashboard(text, dir);

            Assert.Equal("t", doc.Id);
            Assert.Equal("Tab title", doc.Title);
            Assert.Null(doc.ConnectorId);
            Assert.Equal("demo_apps", doc.ColorPalette);
            Assert.Equal("number", doc.Cards[0].Diagram.Kind);
            Assert.Equal("cfg.toml", DashSpecParser.ReadRuntimePath(text));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
