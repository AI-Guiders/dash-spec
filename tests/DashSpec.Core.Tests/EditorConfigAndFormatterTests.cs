using DashSpec.Core.Authoring;
using DashSpec.Core.Authoring.EditorConfig;
using DashSpec.Core.Authoring.Formatting;
using DashSpec.Execution.Parsing;
using DashSpec.Modeling.Parse.Formatting;
using Xunit;
using CoreDashSpecParser = DashSpec.Core.Parsing.DashSpecParser;

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

public sealed class DashSpecBlockFormatterTests
{
    static DashSpecBlockFormatterTests() => DashSpecParser.EnsureModuleParsersRegistered();

    static DashSpecFormatOptions Options =>
        new()
        {
            IndentStyle = "space",
            IndentSize = 4,
            EndOfLine = "lf",
            TrimTrailingWhitespace = true,
            InsertFinalNewline = true,
            DashSpecFormatOnSave = true,
            DashSpecMaxConsecutiveBlankLines = 1,
            DashSpecIndentBlockBody = true,
            DashSpecPreserveBlankLineBeforeEnd = false,
        };

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

        var formatted = DashSpecBlockFormatter.format(input, Options);
        var lines = formatted.Split('\n');

        Assert.Equal("@dashboard demo", lines[0]);
        Assert.Equal("    report", lines[1]);
        Assert.Equal("        card a as \"A\"", lines[2]);
        Assert.Equal("            bind", lines[3]);
        Assert.Equal("                x", lines[4]);
        Assert.Equal("            end bind", lines[5]);
        Assert.Equal("        end card", lines[6]);
        Assert.Equal("    end report", lines[7]);
        Assert.Equal("    end dashboard", lines[8]);
    }

    [Fact]
    public void Format_indents_block_bodies_one_level_deeper_than_opener()
    {
        const string input = """
            @dashboard demo_soak
            runtime
            manifest = "demo.toml"
            end runtime
            configuration
            sqldialect = tsql
            end configuration
            end dashboard
            """;

        var formatted = DashSpecBlockFormatter.format(input, Options);
        var lines = formatted.Split('\n');

        Assert.Equal("@dashboard demo_soak", lines[0]);
        Assert.Equal("    runtime", lines[1]);
        Assert.Equal("        manifest = \"demo.toml\"", lines[2]);
        Assert.Equal("    end runtime", lines[3]);
        Assert.Equal("    configuration", lines[4]);
        Assert.Equal("        sqldialect = tsql", lines[5]);
        Assert.Equal("    end configuration", lines[6]);
        Assert.Equal("    end dashboard", lines[7]);
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
        var doc = CoreDashSpecParser.Parse(formatted, Path.GetDirectoryName(path)!);
        Assert.Equal("demo_soak", doc.Id);
        Assert.True(doc.Cards.Count > 0);
    }
}
