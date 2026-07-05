using DashSpec.Core.Parsing;
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
            File.WriteAllText(Path.Combine(dir, "diagrams", "x.dashdiagram"), """
                @diagram x

                number {
                  value = n
                }
                """);

            const string text = """
                @tab t {
                  runtime { manifest = "cfg.toml" }
                  configuration { sqldialect = tsql }
                  !include "diagrams/x.dashdiagram"
                  wiring { use connector sqlserver }
                  report "Tab title" {
                    standalone {
                      filter field app on dbo.apps.name as "App"
                      toolbar { app }
                    }
                    card c as "C" {
                      diagram x
                      datasource view dbo.t
                      bind app
                    }
                  }
                }
                """;

            var doc = DashSpecParser.Parse(text, dir);

            Assert.Equal("t", doc.Id);
            Assert.Equal("Tab title", doc.Title);
            Assert.Equal("sqlserver", doc.ConnectorId);
            Assert.Equal("number", doc.Cards[0].Diagram.Kind);
            Assert.Equal("cfg.toml", DashSpecParser.ReadRuntimePath(text));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
