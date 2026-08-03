namespace DashSpec.Core.Tests;

/// <summary>Minimal block-module .dashspec snippets for tests (ADR-0036 end-only).</summary>
internal static class BlockSpecTestHelper
{
    public static string Dashboard(
        string reportBody,
        string title = "T",
        string id = "t",
        string? runtimeManifest = null,
        bool connector = false,
        string? wiringExtra = null)
    {
        var lines = new List<string> { $"@dashboard {id}" };

        if (runtimeManifest is not null)
        {
            lines.Add("  runtime");
            lines.Add($"    manifest = \"{runtimeManifest}\"");
            lines.Add("  end runtime");
            lines.Add("  configuration");
            lines.Add("    sqldialect = tsql");
            lines.Add("  end configuration");
        }

        if (connector || wiringExtra is not null)
        {
            lines.Add("  wiring");
            if (connector)
            {
                lines.Add("    use connector sqlserver");
            }

            if (wiringExtra is not null)
            {
                lines.Add($"    {wiringExtra}");
            }

            lines.Add("  end wiring");
        }

        lines.Add("  report");
        lines.Add($"    title = \"{title}\"");
        lines.Add(Indent(reportBody, 4));
        lines.Add("  end report");
        return string.Join('\n', lines);
    }

    public static string Tab(
        string reportBody,
        string id = "extra",
        string? title = null,
        bool connector = false)
    {
        var lines = new List<string> { $"@tab {id}" };
        if (connector)
        {
            lines.Add("  wiring");
            lines.Add("    use connector sqlserver");
            lines.Add("  end wiring");
        }

        lines.Add("  report");
        if (title is not null)
        {
            lines.Add($"    title = \"{title}\"");
        }

        lines.Add(Indent(reportBody, 4));
        lines.Add("  end report");
        return string.Join('\n', lines);
    }

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        return string.Join('\n', text.Split('\n').Select(line => string.IsNullOrWhiteSpace(line) ? line : pad + line));
    }
}
