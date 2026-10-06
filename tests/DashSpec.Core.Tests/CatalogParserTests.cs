using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class CatalogParserTests
{
    [Fact]
    public void Parse_catalog_with_default_and_entries()
    {
        const string text = """
            @catalog demo
            
            default soak
            
            entry soak as "Dev Soak"
              dashspec "demo-soak.dashspec"
            
            entry analytics as "Analytics"
              dashspec "demo-analytics.dashspec"
            """;

        var catalog = CatalogParser.Parse(text);

        Assert.Equal("demo", catalog.Id);
        Assert.Equal("soak", catalog.DefaultEntryId);
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal("Dev Soak", catalog.Entries[0].Title);
        Assert.Equal("demo-soak.dashspec", catalog.Entries[0].DashspecPath);
    }

    [Fact]
    public void Parse_inline_entry_line()
    {
        const string text = """
            @catalog demo
            entry overview as "Overview" dashspec "demo-soak.dashspec"
            """;

        var catalog = CatalogParser.Parse(text);

        Assert.Equal("overview", catalog.DefaultEntryId);
        Assert.Single(catalog.Entries);
    }

    [Fact]
    public void Parse_catalog_entry_initial_tab()
    {
        const string text = """
            @catalog demo
            entry versions as "Версии ПО"
              dashspec "demo-soak.dashspec"
              tab versions
            """;

        var catalog = CatalogParser.Parse(text);

        Assert.Equal("versions", catalog.Entries[0].InitialTabId);
    }

    [Fact]
    public void Parse_rejects_duplicate_entry_id()
    {
        const string text = """
            @catalog x
            entry a dashspec "a.dashspec"
            entry a dashspec "b.dashspec"
            """;

        Assert.Throws<DashSpecParseException>(() => CatalogParser.Parse(text));
    }
}
