using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Execution.Compilation;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Xunit;

namespace DashSpec.Execution.Compilation.Tests;

/// <summary>L2 compile contract tests (ADR-0099 B4 / ADR-0098 import index).</summary>
public sealed class ReportCompilerCompileTests
{
    private static readonly string ImportFixtureDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "Fixtures",
        "import-project"));

    private static readonly string DemoSoakPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "samples", "demo", "demo-soak.dashspec"));

    public ReportCompilerCompileTests() => ExecutionDashSpecParser.EnsureModuleParsersRegistered();

    [Fact]
    public void Compile_demo_project_rejects_legacy_include_envelope()
    {
        var specDir = Path.GetDirectoryName(DemoSoakPath)!;
        const string text = """
            @dashboard legacy_probe
                namespace Demo.Soak
                !include "diagrams/*.dashdiagram"
                runtime
                    manifest = "demo.toml"
                end runtime
                connect
                    use palette demo_apps
                end connect
                report
                    title = "Probe"
                end report
            end dashboard
            """;

        var compiler = new ReportCompiler();
        Assert.Throws<DashSpec.Core.Parsing.DashSpecParseException>(() =>
            compiler.Compile(text, specDir, DashSpecParseOptions.Default));
    }

    [Fact]
    public void Compile_demo_soak_dashboard_via_project_index()
    {
        Assert.True(File.Exists(DemoSoakPath), $"Missing {DemoSoakPath}");
        var text = File.ReadAllText(DemoSoakPath);
        var specDir = Path.GetDirectoryName(DemoSoakPath)!;
        var compiler = new ReportCompiler();
        var result = compiler.Compile(text, specDir, DashSpecParseOptions.Default);

        Assert.Equal("demo_soak", result.Document.Id);
        Assert.Equal(18, result.Document.Cards.Count);
        Assert.True(result.Document.ResolvedModuleDiagrams.ContainsKey("demo_peak_kpi"));
        Assert.DoesNotContain(text, "!include", StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_import_project_rejects_legacy_include_when_index_disables_it()
    {
        const string text = """
            @tab x
            !include "units/*.dashdiagram"
            runtime
            end runtime
            report
              card c as "C"
                view
                  diagram ref demo_activity_5min_line line
                  end line
                end view
              end card
            end report
            end tab x
            """;

        var compiler = new ReportCompiler();
        Assert.Throws<DashSpec.Core.Parsing.DashSpecParseException>(() =>
            compiler.Compile(text, ImportFixtureDir, DashSpecParseOptions.Default));
    }
}
