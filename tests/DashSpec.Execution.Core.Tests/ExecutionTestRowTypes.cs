using DashSpec.Core.Model;
using DashSpec.Execution.Parsing;

namespace DashSpec.Execution.Core.Tests;

internal static class ExecutionTestRowTypes
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    internal static DashboardDocument Parse(string dashspecText, string? specDirectory = null) =>
        DashSpecParser.Parse(dashspecText, specDirectory ?? FixtureDir);
}
