using DashSpec.Core.Layout;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class LusSpecsSyntaxTests
{
    private static DashSpecParseOptions LusParseOptions { get; } = new()
    {
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
        ExtensionBlockPluginIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["views"] = "card_views",
        },
    };

    public static TheoryData<string> LusSpecPaths =>
    [
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-stakeholder.dashspec",
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-overview.dashspec",
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-versions.dashspec",
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

        foreach (var card in doc.Cards)
        {
            if (card.InteriorBoard is null)
            {
                continue;
            }

            _ = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        }
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
        _ = reader.ReadIdent();
        reader.Expect(TokenKind.Eq);
        _ = reader.ReadRestOfLine();
        Assert.True(BlockSyntax.IsBlockEnd(reader, "table"));
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
        var tokens = DashSpecLexer.tokenize(text);
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
    public void Parse_table_diagram_formats_block_serializes_column_formats()
    {
        var text = """
            @diagram t
            table
              columns = host_name, bucket_start_utc
              formats
                bucket_start_utc = datetime.short
              end formats
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("bucket_start_utc:datetime.short", fragment.Diagram!.Properties["column_formats"]);
    }

    [Fact]
    public void Parse_table_diagram_rejects_inline_column_formats()
    {
        var text = """
            @diagram t
            table
              columns = a, b
              column_formats = a:date.short
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("column_formats", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_order_by_with_comma()
    {
        var text = """
            @diagram t
            table
              columns = host_name, user_sam
              order_by = host_name ASC, user_sam ASC
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("host_name ASC, user_sam ASC", fragment.Diagram!.Properties["order_by"]);
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
            Path.GetDirectoryName(path)!);
        Assert.Equal("table", fragment.Diagram!.Kind);
    }
}
