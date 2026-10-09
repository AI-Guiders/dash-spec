using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class CommandAliasesParserTests
{
    [Fact]
    public void Parse_commands_block_maps_aliases_to_filter_ids()
    {
        var document = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard demo
                  !include "query-row-types.dashtype"
              report
              title = "Demo"
              commands
                date = usage_date
                app = app_name
              end commands
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Usage"
                end show
              end filter
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              filters dashboard
              usage_date
              app_name
              end dashboard
              card c as "C"
              bind usage_date, app_name
              diagram number
              value = total
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
            """);

        Assert.Equal("usage_date", document.ResolvedCommandAliases["date"]);
        Assert.Equal("app_name", document.ResolvedCommandAliases["app"]);
    }
}
