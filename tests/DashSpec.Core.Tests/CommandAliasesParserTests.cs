using Xunit;

namespace DashSpec.Core.Tests;

public class CommandAliasesParserTests
{
    [Fact]
    public void Parse_commands_block_maps_aliases_to_filter_ids()
    {
        var document = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard demo
                  import types from Fixtures.QueryRowTypes
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

    [Fact]
    public void Parse_commands_block_supports_table_block_dotted_and_imported_commands_unit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dashspec-commands-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "shared-commands.ds"), """
                module commands shared_cmds
                namespace Custom.Commands

                | alias | target     | shortcut | label        |
                | date  | usage_date | fd       | Период       |
                """);

            var specText = """
                module dashboard demo_cmds
                import commands from Custom.Commands
                import types from Fixtures.QueryRowTypes

                report
                    title = "Demo Commands"
                    commands
                        command app
                            target = app_name
                            shortcut = fa
                            label = "Продукт"
                        end command
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
                """;

            var document = DashSpecTestRowTypes.ParseDashboard(specText, tempDir);
            Assert.Equal("usage_date", document.ResolvedCommandAliases["date"]);
            Assert.Equal("app_name", document.ResolvedCommandAliases["app"]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Shell_board_accepts_command_bar_and_commands_slot_tokens()
    {
        var shellBoard = DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile("""
            module layout shell_with_commands
            namespace Custom.Host
            scope host

            layout grid
                [] [] [] [] [] [] [] [] [] [] [] []
            end grid

            [ brand [] nav help settings ]
            [ command_bar ]
            [ content ]
            """);

        var slots = DashSpec.Execution.Runtime.ShellBoardSlotResolver.Resolve(shellBoard);
        Assert.Contains(slots, s => s.SlotId == DashSpec.Abstractions.Viewer.ViewerShellSlotIds.CommandBar && s.Row == 2);
    }
}
