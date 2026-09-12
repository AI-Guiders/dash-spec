using DashSpec.Execution.Authoring;
using Xunit;

namespace DashSpec.Execution.Core.Tests;

public sealed class SpecFragmentPathsTests
{
    [Fact]
    public void ResolvePath_combines_relative_reference_with_spec_directory()
    {
        var specDir = Path.Combine(Path.GetTempPath(), "dashspec-paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(specDir);
        try
        {
            var resolved = SpecFragmentPaths.ResolvePath("diagrams/util.dashdiagram", specDir);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(specDir, "diagrams/util.dashdiagram")),
                resolved);
        }
        finally
        {
            Directory.Delete(specDir, recursive: true);
        }
    }

    [Fact]
    public void Open_expands_logical_import_graph()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dashspec-exec-project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(workspace, "diagrams"));
        try
        {
            File.WriteAllText(Path.Combine(workspace, "diagrams", "util.dashdiagram"), """
                @diagram util
                bar
                  category = app_name
                  value = utilization_pct
                end bar
                """);

            File.WriteAllText(Path.Combine(workspace, "main.dashspec"), """
                @dashboard t
                  import "diagrams/util.dashdiagram"
                  report
                  title = "T"
                  card c as "C"
                  diagram util
                  datasource view dbo.t
                  end card
                  end report
                end dashboard
                """);

            var result = DashSpecProject.Open(workspace, Path.Combine(workspace, "main.dashspec"));

            Assert.Empty(result.Diagnostics);
            Assert.NotNull(result.Project);
            Assert.True(result.Project!.Documents.Count >= 2);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
