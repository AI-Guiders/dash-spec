using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class LusSpecsSyntaxTests
{
    private static DashSpecParseOptions LusParseOptions { get; } = new()
    {
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
    };

    public static TheoryData<string> LusSpecPaths =>
    [
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-stakeholder.dashspec",
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-overview.dashspec",
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-detail.dashspec",
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-soak.dashspec",
    ];

    [Theory]
    [MemberData(nameof(LusSpecPaths))]
    public void Parse_lus_dev_specs(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var doc = DashSpecParser.Parse(text, Path.GetDirectoryName(path)!, LusParseOptions);
        Assert.NotEmpty(doc.Filters);
    }

    [Fact]
    public void Parse_table_diagram_columns_only()
    {
        var text = """
            @diagram t
            table
              columns = a, b
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("table", fragment.Diagram!.Kind);
    }

    [Fact]
    public void IsBlockEnd_after_order_by_rest_of_line()
    {
        var reader = ParserUtilities.CreateReader("order_by = occurred_at_utc\nend table\n");
        _ = reader.ReadPropertyKey();
        reader.Expect(TokenKind.Eq);
        _ = reader.ReadRestOfLine();
        Assert.True(BlockSyntax.IsBlockEnd(reader, "table"));
    }

    [Fact]
    public void Parse_table_property_block_order_by_only()
    {
        var text = """
            table
              order_by = occurred_at_utc
            end table
            """;

        var reader = ParserUtilities.CreateReader(text);
        _ = reader.ReadIdent();
        var props = PropertyBlockParser.Parse(
            reader,
            DiagramKindRegistry.GetProperties("table"),
            "diagram table");

        Assert.Equal("occurred_at_utc", props["order_by"]);
    }

    [Fact]
    public void Parse_table_property_block_columns_only()
    {
        var text = """
            table
              columns = a, b
            end table
            """;

        var reader = ParserUtilities.CreateReader(text);
        _ = reader.ReadIdent();
        var props = PropertyBlockParser.Parse(
            reader,
            DiagramKindRegistry.GetProperties("table"),
            "diagram table");

        Assert.Equal("a, b", props["columns"]);
    }

    [Fact]
    public void Parse_table_property_block_with_order_by()
    {
        var text = """
            table
              columns = a, b
              order_by = occurred_at_utc
            end table
            """;

        var reader = ParserUtilities.CreateReader(text);
        _ = reader.ReadIdent();
        var props = PropertyBlockParser.Parse(
            reader,
            DiagramKindRegistry.GetProperties("table"),
            "diagram table");

        Assert.Equal("occurred_at_utc", props["order_by"]);
    }

    [Fact]
    public void Parse_table_diagram_order_by_inline()
    {
        var text = """
            @diagram t
            table
              columns = a, b
              order_by = occurred_at_utc
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("occurred_at_utc", fragment.Diagram!.Properties["order_by"]);
    }

    [Fact]
    public void Tokenize_table_order_by()
    {
        var text = "order_by = occurred_at_utc\nend table\n";
        var tokens = DashSpecLexer.Tokenize(text);
        Assert.Contains(tokens, t => t.Value == "order_by");
        Assert.Contains(tokens, t => t.Value == "end");
        Assert.Contains(tokens, t => t.Value == "table");
    }

    [Fact]
    public void Parse_table_diagram_order_by_without_desc()
    {
        var text = """
            @diagram t
            table
              columns = a, b
              order_by = occurred_at_utc
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("occurred_at_utc", fragment.Diagram!.Properties["order_by"]);
    }

    [Fact]
    public void Parse_table_diagram_with_order_by()
    {
        var text = """
            @diagram t
            table
              columns = occurred_at_utc, app_name
              order_by = occurred_at_utc DESC
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("table", fragment.Diagram!.Kind);
        Assert.Equal("occurred_at_utc DESC", fragment.Diagram.Properties["order_by"]);
    }

    [Fact]
    public void Parse_lus_events_detail_table_diagram()
    {
        var path = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\diagrams\detail\events-detail-table.dashdiagram";
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(
            text,
            @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec");
        Assert.Equal("table", fragment.Diagram!.Kind);
    }
}
