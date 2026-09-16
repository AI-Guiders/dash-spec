using DashSpec.Core.Authoring;
using DashSpec.Core.Authoring.Formatting;
using DashSpec.Core.Authoring.EditorConfig;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class EditorConfigResolverTests
{
    [Fact]
    public void ResolveForFile_applies_nearest_editorconfig_section()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashspec-editorconfig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var nested = Path.Combine(root, "reports");
        Directory.CreateDirectory(nested);

        File.WriteAllText(Path.Combine(root, ".editorconfig"), """
            root = true

            [*]
            indent_size = 4

            [*.dashspec]
            indent_size = 2
            dashspec_max_consecutive_blank_lines = 2
            """);

        File.WriteAllText(Path.Combine(nested, ".editorconfig"), """
            [*.dashspec]
            indent_size = 3
            """);

        var target = Path.Combine(nested, "report.dashspec");
        File.WriteAllText(target, "@dashboard x\nend dashboard\n");

        var options = EditorConfigResolver.ResolveForFile(target, root);
        Assert.Equal(3, options.IndentSize);
        Assert.Equal(2, options.DashSpecMaxConsecutiveBlankLines);
    }
}

public sealed class DashSpecTextFormatterTests
{
    [Fact]
    public void Format_reindents_basic_like_blocks()
    {
        const string input = """
            @dashboard demo
            report
            card a as "A"
            bind
            x
            end bind
            end card
            end report
            end dashboard
            """;

        var options = EditorConfigOptions.Default with { DashSpecPreserveBlankLineBeforeEnd = false };
        var formatted = DashSpecTextFormatter.Format(input, options);
        var lines = formatted.Split('\n');

        Assert.Equal("@dashboard demo", lines[0]);
        Assert.Equal("    report", lines[1]);
        Assert.Equal("    card a as \"A\"", lines[2]);
        Assert.Equal("    bind", lines[3]);
        Assert.Equal("        x", lines[4]);
        Assert.Equal("    end bind", lines[5]);
        Assert.Equal("    end card", lines[6]);
        Assert.Equal("    end report", lines[7]);
        Assert.Equal("    end dashboard", lines[8]);
    }

    [Fact]
    public void Format_demo_soak_still_parses()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-soak.dashspec"));

        var text = File.ReadAllText(path);
        var formatted = DashSpecDocumentPipeline.Format(text, path, Path.GetDirectoryName(path));
        var doc = DashSpecParser.Parse(formatted, Path.GetDirectoryName(path)!);
        Assert.Equal("demo_soak", doc.Id);
        Assert.True(doc.Cards.Count > 0);
    }
}



