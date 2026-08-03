using DashSpec.Core.Parsing;
using DashSpec.Core.Validation;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecDiagnosticServiceTests
{
    [Fact]
    public void ValidateText_DiagramModule_ReturnsEmptyOnValid()
    {
        const string text = """
            @diagram sample
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
            @diagram sample
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
        var path = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-soak.dashspec";
        if (!File.Exists(path))
        {
            return;
        }

        var diagnostics = DashSpecDiagnosticService.ValidateFile(path);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateText_DetailTab_NoFalsePositiveOnValidFile()
    {
        var path = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-detail.dashspec";
        if (!File.Exists(path))
        {
            return;
        }

        var diagnostics = DashSpecDiagnosticService.ValidateFile(path);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateText_IncompleteInclude_EditorMode_NoIncludeNotFoundDiagnostic()
    {
        const string text = """
            @tab sample

            !include "layouts/"
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

            Assert.DoesNotContain(
                diagnostics,
                diagnostic => diagnostic.Message.Contains("!include not found", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(specDirectory, recursive: true);
        }
    }

    [Fact]
    public void ValidateText_MissingInclude_PointsToIncludeLine()
    {
        const string text = """
            @tab sample

            runtime
              manifest = "x.toml"
            end runtime

            !include "layouts/missing.dashlayout"
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
            Assert.Equal(6, diagnostics[0].Line);
            Assert.Contains("!include not found", diagnostics[0].Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(specDirectory, recursive: true);
        }
    }
}
