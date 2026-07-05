namespace DashSpec.Core.Tests;

/// <summary>Minimal block-module .dashspec snippets for tests (ADR-0024).</summary>
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
        var lines = new List<string> { $"@dashboard {id} {{" };

        if (runtimeManifest is not null)
        {
            lines.Add($"  runtime {{ manifest = \"{runtimeManifest}\" }}");
            lines.Add("  configuration { sqldialect = tsql }");
        }

        if (connector || wiringExtra is not null)
        {
            lines.Add("  wiring {");
            if (connector)
            {
                lines.Add("    use connector sqlserver");
            }

            if (wiringExtra is not null)
            {
                lines.Add($"    {wiringExtra}");
            }

            lines.Add("  }");
        }

        lines.Add($"  report \"{title}\" {{");
        lines.Add(Indent(reportBody, 4));
        lines.Add("  }");
        lines.Add("}");
        return string.Join('\n', lines);
    }

    public static string Tab(
        string reportBody,
        string id = "extra",
        string? title = null,
        bool connector = false)
    {
        var lines = new List<string> { $"@tab {id} {{" };
        if (connector)
        {
            lines.Add("  wiring { use connector sqlserver }");
        }

        if (title is null)
        {
            lines.Add("  report {");
        }
        else
        {
            lines.Add($"  report \"{title}\" {{");
        }

        lines.Add(Indent(reportBody, 4));
        lines.Add("  }");
        lines.Add("}");
        return string.Join('\n', lines);
    }

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        return string.Join('\n', text.Split('\n').Select(line => string.IsNullOrWhiteSpace(line) ? line : pad + line));
    }
}
