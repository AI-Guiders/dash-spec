using DashSpec.Execution.Runtime;
using DashSpec.Execution.Parsing;
using Xunit;

using DashSpec.Compiler.Contract;

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

    /// <summary>Repo-relative sample root: the test host runs from tests/&lt;project&gt;/bin/&lt;cfg&gt;/&lt;tfm&gt; (five levels up).</summary>
    private static readonly string SamplesDemo = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "samples", "demo"));

    /// <summary>
    /// Standalone dashboard specs. <c>demo-analytics.ds</c> is a tab unit (ADR-0101): its cards bind filters
    /// owned by the dashboard, so it is parsed through <c>demo-soak.ds</c> (<c>import tabs from Demo.Analytics</c>),
    /// not on its own.
    /// </summary>
    public static TheoryData<string> DemoSpecPaths =>
    [
        Path.Combine(SamplesDemo, "demo-soak.ds"),
    ];

    [Theory]
    [MemberData(nameof(DemoSpecPaths))]
    public void Parse_demo_specs(string path)
    {
        var text = File.ReadAllText(path);
        var doc = DashSpecTestRowTypes.ParseDashboard(text, Path.GetDirectoryName(path)!, DemoParseOptions);
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
            module diagram t
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
            module diagram t
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
            module diagram t
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
            module diagram t
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
            module diagram t
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
            module diagram t
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
    public void Parse_table_diagram_columns_pipe_table_desugars_all_attributes()
    {
        var text = """
            module diagram t
            table
              columns
                | id                    | format         | label        | sorting |
                | host_name             | -              | Хост         | ASC     |
                | user_sam              |                | Пользователь | ASC     |
                | peak_bucket_start_utc | datetime.short | Момент пика  | DESC    |
              end columns
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        var props = fragment.Diagram!.Properties;
        Assert.Equal("host_name, user_sam, peak_bucket_start_utc", props["columns"]);
        Assert.Equal("peak_bucket_start_utc:datetime.short", props["column_formats"]);
        Assert.Equal("host_name = Хост\nuser_sam = Пользователь\npeak_bucket_start_utc = Момент пика", props["column_labels"]);
        Assert.Equal("host_name ASC, user_sam ASC, peak_bucket_start_utc DESC", props["order_by"]);
    }

    [Fact]
    public void Parse_table_diagram_title_with_placeholders_keeps_columns_desugaring()
    {
        var text = """
            module diagram t
            table
              title = "Запуски: {app_name}"
              columns
                | id         | format     | label        | sorting |
                | usage_date | date.short | Дата         | DESC    |
                | user_sam   | -          | Пользователь | -       |
                | launches   | -          | Запуски      | DESC    |
              end columns
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        var props = fragment.Diagram!.Properties;
        Assert.Equal("table", fragment.Diagram.Kind);
        Assert.Equal("Запуски: {app_name}", props["title"]);
        Assert.Equal("usage_date, user_sam, launches", props["columns"]);
        Assert.Equal("usage_date:date.short", props["column_formats"]);
        Assert.Equal("usage_date DESC, launches DESC", props["order_by"]);
    }

    [Fact]
    public void Parse_table_diagram_columns_alignment_desugars_maps()
    {
        var text = """
            module diagram t
            table
              columns
                | id         | format     | label        | sorting | alignment | header_alignment |
                | usage_date | date.short | Дата         | DESC    | center    | left             |
                | launches   | -          | Запуски      | DESC    | right     |                  |
              end columns
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        var props = fragment.Diagram!.Properties;
        Assert.Equal("usage_date, launches", props["columns"]);
        Assert.Equal("usage_date:center, launches:right", props["column_alignments"]);
        Assert.Equal("usage_date:left", props["column_header_alignments"]);
    }

    [Fact]
    public void Parse_table_diagram_columns_rejects_unknown_alignment()
    {
        var text = """
            module diagram t
            table
              columns
                | id       | format | label   | sorting | alignment |
                | launches | -      | Запуски | DESC    | middle    |
              end columns
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("alignment", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("middle", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_accepts_subset_header_and_skips_separator_rows()
    {
        var text = """
            module diagram t
            table
              columns
                | id                | label |
                |-------------------|-------|
                | host_name         | Хост  |
                | bucket_start_utc | |
              end columns
            end table
            """;

        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!);
        var props = fragment.Diagram!.Properties;
        Assert.Equal("host_name, bucket_start_utc", props["columns"]);
        Assert.Equal("host_name = Хост", props["column_labels"]);
        Assert.False(props.ContainsKey("column_formats"));
        Assert.False(props.ContainsKey("order_by"));
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_rejects_flat_columns_double_declaration()
    {
        var text = """
            module diagram t
            table
              columns = host_name
              columns
                | id | label |
                | host_name | Хост |
              end columns
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("columns", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_rejects_flat_order_by_with_sorting()
    {
        var text = """
            module diagram t
            table
              columns
                | id | sorting |
                | host_name | ASC |
              end columns
              order_by = host_name ASC
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("order_by", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_rejects_formats_block_with_format_attribute()
    {
        var text = """
            module diagram t
            table
              formats
                host_name = datetime.short
              end formats
              columns
                | id | format |
                | host_name | date.short |
              end columns
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("format", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_rejects_unknown_attribute()
    {
        var text = """
            module diagram t
            table
              columns
                | id | width |
                | host_name | 12 |
              end columns
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("width", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_rejects_bad_sorting_value()
    {
        var text = """
            module diagram t
            table
              columns
                | id | sorting |
                | host_name | RANDOM |
              end columns
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("ASC", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_columns_pipe_table_requires_id_cell()
    {
        var text = """
            module diagram t
            table
              columns
                | id | label |
                |   | Хост |
              end columns
            end table
            """;

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DiagramModuleParser.ParseDiagramFileWithId(text, baseDirectory: null!));
        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_table_diagram_order_by_with_comma()
    {
        var text = """
            module diagram t
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
        var path = Path.Combine(SamplesDemo, "diagrams", "events-detail-table.ds");

        // The bridge must blank the ADR-0098 module header too, exactly like the import path does — a real
        // `.ds` diagram unit carries `namespace` on its second line.
        var text = File.ReadAllText(path);
        var (_, fragment) = DiagramModuleParser.ParseDiagramFileWithId(
            text,
            Path.GetDirectoryName(path)!);
        Assert.Equal("table", fragment.Diagram!.Kind);
    }
}
