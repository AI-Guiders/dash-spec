using DashSpec.Core.Parsing;
using Xunit;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Core.Tests;

public sealed class DemoSoakParseDiagnosticTests
{
    static string DemoDir => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "demo"));

    [Theory]
    [InlineData("header_only")]
    [InlineData("with_diagram_include")]
    [InlineData("with_types_include")]
    [InlineData("with_connect")]
    [InlineData("full_file")]
    public void Parse_demo_soak_slices(string slice)
    {
        var dir = DemoDir;
        var full = File.ReadAllText(Path.Combine(dir, "demo-soak.dashspec"));
        var text = slice switch
        {
            "header_only" => """
                @dashboard demo_soak
                    runtime
                        manifest = "demo.toml"
                    end runtime
                    configuration
                        sqldialect = tsql
                        palette = "palettes/demo-apps.dashpalette"
                    end configuration
                end dashboard
                """,
            "with_diagram_include" => """
                @dashboard demo_soak
                    configuration
                        sqldialect = tsql
                        palette = "palettes/demo-apps.dashpalette"
                    end configuration
                    !include "diagrams/*.dashdiagram"
                end dashboard
                """,
            "with_types_include" => """
                @dashboard demo_soak
                    configuration
                        sqldialect = tsql
                    end configuration
                    !include "demo-rows.dashtype"
                end dashboard
                """,
            "with_connect" => """
                @dashboard demo_soak
                    configuration
                        sqldialect = tsql
                    end configuration
                    connect
                        use palette demo_apps
                        layout grid
                            columns = 12
                            gap = 16
                        end grid
                    end connect
                end dashboard
                """,
            "full_file" => full,
            _ => full,
        };

        var ex = Record.Exception(() => DashSpecParser.Parse(text, dir));
        Assert.Null(ex);
    }
}
