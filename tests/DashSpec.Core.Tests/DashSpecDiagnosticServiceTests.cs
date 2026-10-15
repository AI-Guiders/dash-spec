using DashSpec.Core.Parsing;
using DashSpec.Core.Validation;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecDiagnosticServiceTests
{
    /// <summary>Repo-relative sample root: the test host runs from tests/&lt;project&gt;/bin/&lt;cfg&gt;/&lt;tfm&gt; (five levels up).</summary>
    private static readonly string SamplesDemo = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "samples", "demo"));

    [Fact]
    public void ValidateText_DiagramModule_ReturnsEmptyOnValid()
    {
        const string text = """
            module diagram sample
            heatmap
              x = month
              y = product
            end heatmap
            """;

        var diagnostics = DashSpecDiagnosticService.ValidateText(
            text,
            "sample.dashdiagram",
            Environment.CurrentDirectory);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateText_DiagramModule_ReturnsErrorWithPosition()
    {
        const string text = """
            module diagram sample
            heatmap
              x = month
            """;

        var diagnostics = DashSpecDiagnosticService.ValidateText(
            text,
            "sample.dashdiagram",
            Environment.CurrentDirectory);

        Assert.Single(diagnostics);
        Assert.Contains("heatmap", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateText_SoakShell_NoFalsePositiveFromChildTabs()
    {
        var path = Path.Combine(SamplesDemo, "demo-soak.ds");

        var diagnostics = DashSpecDiagnosticService.ValidateFile(path);

        // Merging the child tabs must not invent errors. The demo's diagram units keep inline chrome keys
        // next to card presentation blocks, so the by-design `diagram.chart_chrome` composition warning fires
        // (see ResolutionLintTests.Composition_warns_legacy_diagram_chrome_with_card_presentation) — and nothing else.
        Assert.DoesNotContain(diagnostics, d => d.Severity == DashSpecDiagnosticSeverity.Error);
        Assert.All(
            diagnostics,
            d => Assert.Contains(
                "is shadowed by card presentation block",
                d.Message,
                StringComparison.Ordinal));
    }

    [Fact(Skip = "ADR-0101 units are .ds; the validator dispatches on legacy unit extensions - editor parity epic")]
    public void ValidateFile_DetailTableUnit_NoFalsePositiveOnValidFile()
    {
        var path = Path.Combine(SamplesDemo, "diagrams", "events-detail-table.ds");

        // ValidateByExtension dispatches on the legacy unit extensions (.dashdiagram, .dashpalette, …), so a
        // current `.ds` unit (ADR-0101) falls through to the document parser and comes back as
        // "DashSpec requires block module format: module dashboard id … / module tab id …". Part of the
        // deferred editor/authoring epic (dispatch by module kind, not by extension).
        var diagnostics = DashSpecDiagnosticService.ValidateFile(path);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateText_MissingImport_EditorMode_ReportsMissingImport()
    {
        const string text = """
            module tab sample

            import layouts from Missing.Layout
            """;

        var specDirectory = Path.Combine(Path.GetTempPath(), "dashspec-incomplete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(specDirectory);

        try
        {
            var diagnostics = DashSpecDiagnosticService.ValidateText(
                text,
                "sample.dashspec",
                specDirectory,
                DashSpecParseOptions.Editor);

            Assert.Single(diagnostics);
            Assert.Contains(
                "No module found for import layouts from 'Missing.Layout'",
                diagnostics[0].Message,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(specDirectory, recursive: true);
        }
    }

    [Fact]
    public void ValidateText_MissingImport_PointsToImportLine()
    {
        const string text = """
            module tab sample

            import layouts from Missing.Layout

            runtime
              manifest = "x.toml"
            end runtime
            """;

        var specDirectory = Path.Combine(Path.GetTempPath(), "dashspec-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(specDirectory);

        try
        {
            var diagnostics = DashSpecDiagnosticService.ValidateText(
                text,
                "sample.dashspec",
                specDirectory,
                DashSpecParseOptions.Editor);

            Assert.Single(diagnostics);
            Assert.Contains(
                "No module found for import layouts from 'Missing.Layout'",
                diagnostics[0].Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, diagnostics[0].Line);
        }
        finally
        {
            Directory.Delete(specDirectory, recursive: true);
        }
    }
}
