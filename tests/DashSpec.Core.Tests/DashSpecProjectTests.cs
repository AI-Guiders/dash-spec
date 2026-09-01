using DashSpec.Core.Authoring;
using DashSpec.Core.Parsing;
using AIGuiders.Platform.Authoring.Core;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecProjectTests
{
    [Fact]
    public void Open_expands_logical_import_graph()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dashspec-project-" + Guid.NewGuid().ToString("N"));
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

            var dashspec = Path.Combine(workspace, "main.dashspec");
            var result = DashSpecProject.Open(workspace, dashspec);

            Assert.Empty(result.Diagnostics);
            Assert.NotNull(result.Project);
            Assert.True(result.Project!.Documents.Count >= 2);
            Assert.Contains(
                result.Project.Documents,
                d => d.Ref.Kind == AuthoringDocumentKind.LogicalFile
                    && d.Ref.Path.Contains("diagrams/util", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void Parse_accepts_import_keyword_alias_for_include()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "diagrams", "activity.dashdiagram"), """
                @diagram activity
                bar
                  category = app_name
                  value = utilization_pct
                end bar
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t
                  import "diagrams/activity.dashdiagram"
                  report
                  title = "T"
                  card c as "C"
                  diagram activity
                  datasource view dbo.t
                  end card
                  end report
                end dashboard
                """, dir);

            Assert.Single(doc.Cards);
            Assert.Equal("bar", doc.Cards[0].Diagram.Kind);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
