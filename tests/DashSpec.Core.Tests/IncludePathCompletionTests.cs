using DashSpec.Core.Validation;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class IncludePathCompletionTests
{
    [Fact]
    public void Suggest_layouts_partial_returns_layout_files()
    {
        var specDir = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec";
        if (!Directory.Exists(specDir))
        {
            return;
        }

        var suggestions = IncludePathCompletion.Suggest(specDir, "layouts/");
        Assert.Contains(suggestions, s => s.Contains("soak-toolbar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Suggest_empty_includes_top_level_directories()
    {
        var specDir = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec";
        if (!Directory.Exists(specDir))
        {
            return;
        }

        var suggestions = IncludePathCompletion.Suggest(specDir, string.Empty);
        Assert.Contains(suggestions, s => s.Equals("layouts/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(suggestions, s => s.Equals("diagrams/", StringComparison.OrdinalIgnoreCase));
    }
}
