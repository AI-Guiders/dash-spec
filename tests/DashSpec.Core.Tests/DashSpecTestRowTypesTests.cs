using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class DashSpecTestRowTypesTests
{
    [Fact]
    public void Parse_source_requires_explicit_ports()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        DashSpecTestRowTypes.SeedFixtureTypesDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "broken.dashflow"), """
            @flow broken
            source s {
              use provider infer
              from view dbo.t
            }
            end flow
            """);

        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard(
            """
            @dashboard t
                  !include "query-row-types.dashtype"
              connect
              flow "broken.dashflow"
              end connect
              report
              title = "T"
              card x as "X"
              diagram bar
              x = a y
              end bar
              data flow { s [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
            """,
            dir));

        Assert.Contains("ports", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
