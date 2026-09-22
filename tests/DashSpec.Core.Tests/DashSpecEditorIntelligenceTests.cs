using DashSpec.Core.Validation;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecEditorIntelligenceTests
{
    [Fact]
    public void GetCompletions_diagram_context_uses_workspace_index()
    {
        var index = new DashSpecWorkspaceIndex();
        index.RegisterDiagram("sales_chart", Path.Combine(Path.GetTempPath(), "sales.dashdiagram"));

        var text = """
            @dashboard demo
              diagram sa
            end dashboard
            """;

        var items = DashSpecEditorIntelligence.GetCompletions(
            "demo.dashspec",
            text,
            line: 2,
            column: 14,
            index);

        Assert.Contains(items, item => item.Label.Equals("sales_chart", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_keyword_prefix_filters_dashboard_tokens()
    {
        var text = "@dashboard demo\nend dashboard\n";
        var items = DashSpecEditorIntelligence.GetCompletions("demo.dashspec", text, line: 1, column: 2);
        Assert.Contains(items, item => item.Label.Equals("@dashboard", StringComparison.Ordinal));
    }
}
