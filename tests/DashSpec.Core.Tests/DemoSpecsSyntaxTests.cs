using DashSpec.Core.Layout;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class DemoSpecsSyntaxTests
{
    private static DashSpecParseOptions DemoParseOptions { get; } = new()
    {
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
        ExtensionBlockPluginIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["views"] = "card_views",
        },
    };

    public static TheoryData<string> DemoSpecPaths =>
    [
        @"samples/demo\demo-stakeholder.dashspec",
        @"samples/demo\demo-overview.dashspec",
        @"samples/demo\demo-versions.dashspec",
        @"samples/demo\demo-detail.dashspec",
        @"samples/demo\demo-soak.dashspec",
    ];

    [Theory]
    [MemberData(nameof(DemoSpecPaths))]
    public void Parse_demo_specs(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var doc = DashSpecParser.Parse(text, Path.GetDirectoryName(path)!, DemoParseOptions);
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
    public void Parse_table_diagram_accepts_legacy_inline_column_formats()
    {
        var text = """
            @diagram t
            table
              columns = a, b
              column_formats = a:date.short
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        Assert.Equal("a:date.short", fragment.Diagram!.Properties["column_formats"]);
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
    public void Parse_demo_events_detail_table_diagram()
    {
        var path = @"samples/demo\diagrams\detail\events-detail-table.dashdiagram";
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
