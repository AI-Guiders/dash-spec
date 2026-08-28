using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class CommandAliasesParserTests
{
    [Fact]
    public void Parse_commands_block_maps_aliases_to_filter_ids()
    {
        var document = DashSpecParser.Parse("""
            @dashboard demo
              report
              title = "Demo"
              commands
                date = usage_date
                app = app_name
              end commands
              filter date usage_date on usage_date as "Usage" default -7d..today
              filter field app_name on app_name as "App"
              filters dashboard
              usage_date
              app_name
              end dashboard
              card c as "C"
              bind usage_date, app_name
              diagram number
              value = total
              end number
              datasource view dbo.t
              end card
              end report
            end dashboard
            """);

        Assert.Equal("usage_date", document.ResolvedCommandAliases["date"]);
        Assert.Equal("app_name", document.ResolvedCommandAliases["app"]);
    }
}
