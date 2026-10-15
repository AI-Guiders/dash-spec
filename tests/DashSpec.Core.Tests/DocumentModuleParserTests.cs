using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class DocumentModuleParserTests
{
    [Fact(Skip = "ADR-0102: include/legacy syntax removed; covered by import tests - to rewrite")]
    public void Parse_block_tab_module_with_standalone_filters_and_diagram_ref()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-block-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "diagrams", "x.dashdiagram"), """
                module diagram x
                
                number
                  value = n
                end number
                """);

            const string text = """
                module tab t
                  !include "diagrams/x.dashdiagram"
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
