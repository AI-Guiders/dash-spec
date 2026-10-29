using DashSpec.Execution.Compilation;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecEditorIntelligenceTests
    : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dashspec-lrc-" + Guid.NewGuid().ToString("N"));

    public DashSpecEditorIntelligenceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void GetCompletions_diagram_context_uses_workspace_index()
    {
        var index = new DashSpecWorkspaceIndex();
        index.RegisterDiagram("sales_chart", Path.Combine(Path.GetTempPath(), "sales.dashdiagram"));

        var text = """
            module dashboard demo
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
    public void GetCompletions_diagram_ref_slot_form_completes_diagram_ids()
    {
        var index = new DashSpecWorkspaceIndex();
        index.RegisterDiagram("lus_users_app_list", Path.Combine(Path.GetTempPath(), "app-list.ds"));

        var text = """
            module section users
            report
              card app_users as "Apps"
                diagram ref products lus_us
              end card
            end report
            end section
            """;

        // The living slot-ref form: `diagram ref <slot> <id prefix…>`.
        var items = DashSpecEditorIntelligence.GetCompletions(
            "users.ds",
            text,
            line: 4,
            column: 44,
            index);

        Assert.Contains(items, item => item.Label.Equals("lus_users_app_list", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_use_flow_completes_flow_ids()
    {
        var index = new DashSpecWorkspaceIndex();
        index.RegisterUnit(new DashSpecUnitDeclaration("flow", "LicenseUsageReport", Path.Combine(Path.GetTempPath(), "lus-report.ds"), 1));

        var text = """
            module section users
            report
              card app_users as "Apps"
                connect
                  use flow Lic
                end connect
              end card
            end report
            end section
            """;

        var items = DashSpecEditorIntelligence.GetCompletions(
            "users.ds",
            text,
            line: 5,
            column: 31,
            index);

        Assert.Contains(items, item => item.Label.Equals("LicenseUsageReport", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletions_keyword_prefix_filters_dashboard_tokens()
    {
        var text = "module dashboard demo\nend dashboard\n";
        var items = DashSpecEditorIntelligence.GetCompletions("demo.dashspec", text, line: 1, column: 2);
        Assert.Contains(items, item => item.Label.Equals("module dashboard", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_registers_every_module_kind_with_line_and_namespace()
    {
        Write("flows/lus-report.ds", """
            module flow LicenseUsageReport
            namespace LicenseUsageReport

            source fixture_src {
              from view dbo.t
            }
            """);
        Write("diagrams/overview/app-list.ds", """
            module diagram lus_users_app_list
            namespace Luf.Users.Diagrams

            table
              columns = app_name
            end table
            """);
        var dashboard = Write("lus-dev-users.ds", """
            module section users
            namespace Luf.Users

            report
              title = "Пользователи"
            end report
            """);

        var index = DashSpecWorkspaceIndex.Scan(_dir);

        Assert.Equal(3, index.Units.Count);
        var flow = index.UnitsById["LicenseUsageReport"];
        Assert.Equal("flow", flow.Kind);
        Assert.Equal(1, flow.Line);
        Assert.Contains(index.UnitsByNamespace["LicenseUsageReport"], u => u.Path == flow.Path);

        var diagram = index.UnitsById["lus_users_app_list"];
        Assert.Equal("diagram", diagram.Kind);
        Assert.Equal("Luf.Users.Diagrams", diagram.Namespace);
        Assert.EndsWith("app-list.ds", diagram.Path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

        var tab = index.UnitsById["users"];
        Assert.Equal("section", tab.Kind);
        Assert.True(File.Exists(tab.Path));
        Assert.Equal(dashboard, tab.Path, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetDefinition_resolves_diagram_flow_and_namespace_across_files()
    {
        var flowPath = Write("flows/lus-report.ds", """
            module flow LicenseUsageReport
            namespace LicenseUsageReport
            """);
        var diagramPath = Write("diagrams/app-list.ds", """
            module diagram lus_users_app_list
            namespace Luf.Users.Diagrams
            """);
        Write("lus-dev-users.ds", """
            module section users
            namespace Luf.Users

            import diagrams from Luf.Users.Diagrams
            import flows from LicenseUsageReport

            report
              card app_users as "Apps"
                connect
                  use flow LicenseUsageReport
                end connect
                diagram ref products lus_users_app_list
              end card
            end report
            """);

        var index = DashSpecWorkspaceIndex.Scan(_dir);
        var path = Path.Combine(_dir, "lus-dev-users.ds");
        var text = File.ReadAllText(path);

        // `diagram ref <slot> <id>` → the diagram module file.
        var diagramDef = DashSpecEditorIntelligence.TryGetDefinition(
            path, text, line: 12, column: 30, index);
        Assert.NotNull(diagramDef);
        Assert.Equal(
            diagramPath.Replace('\\', '/'),
            diagramDef.FilePath.Replace('\\', '/'),
            StringComparer.OrdinalIgnoreCase);

        // `use flow <id>` → the flow module file.
        var flowDef = DashSpecEditorIntelligence.TryGetDefinition(
            path, text, line: 10, column: 18, index);
        Assert.NotNull(flowDef);
        Assert.Equal(
            flowPath.Replace('\\', '/'),
            flowDef.FilePath.Replace('\\', '/'),
            StringComparer.OrdinalIgnoreCase);

        // `import flows from <Namespace>` → the unit file declaring the namespace.
        var nsDef = DashSpecEditorIntelligence.TryGetDefinition(
            path, text, line: 5, column: 24, index);
        Assert.NotNull(nsDef);
        Assert.Equal(
            flowPath.Replace('\\', '/'),
            nsDef.FilePath.Replace('\\', '/'),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindWorkspaceUsages_finds_diagram_usages_across_files()
    {
        Write("diagrams/app-list.ds", """
            module diagram lus_users_app_list
            namespace Luf.Users.Diagrams
            """);
        Write("lus-dev-users.ds", """
            module section users

            report
              card app_users as "Apps"
                diagram ref products lus_users_app_list
              end card
              card vendor_users as "Vendors"
                diagram ref vendors lus_users_vendor_list
              end card
            end report
            """);

        var index = DashSpecWorkspaceIndex.Scan(_dir);

        var usages = DashSpecEditorIntelligence.FindWorkspaceUsages("lus_users_app_list", index);

        Assert.Equal(2, usages.Count);
        Assert.Contains(usages, u => u.Path.Replace('\\', '/').EndsWith("diagrams/app-list.ds", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(usages, u => u.Path.Replace('\\', '/').EndsWith("lus-dev-users.ds", StringComparison.OrdinalIgnoreCase));
    }
}

