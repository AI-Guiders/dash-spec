using DashSpec.Execution.Parsing;
using Xunit;

using DashSpec.Execution.Runtime;

namespace DashSpec.Core.Tests;

public sealed class CatalogParserTests
{
    [Fact]
    public void Parse_catalog_with_default_and_entries()
    {
        const string text = """
            module catalog demo
            
            default soak
            
            entry soak as "Dev Soak"
              dashspec "demo-soak.ds"
            
            entry analytics as "Analytics"
              dashspec "demo-analytics.ds"
            """;

        var catalog = CatalogReader.Parse(text);

        Assert.Equal("demo", catalog.Id);
        Assert.Equal("soak", catalog.DefaultEntryId);
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal("Dev Soak", catalog.Entries[0].Title);
        Assert.Equal("demo-soak.ds", catalog.Entries[0].DashspecPath);
    }

    [Fact]
    public void Parse_inline_entry_line()
    {
        const string text = """
            module catalog demo
            entry overview as "Overview" dashspec "demo-soak.ds"
            """;

        var catalog = CatalogReader.Parse(text);

        Assert.Equal("overview", catalog.DefaultEntryId);
        Assert.Single(catalog.Entries);
    }

    [Fact]
    public void Parse_catalog_entry_initial_tab()
    {
        const string text = """
            module catalog demo
            entry versions as "Версии ПО"
              dashspec "demo-soak.ds"
              section versions
            """;

        var catalog = CatalogReader.Parse(text);

        Assert.Equal("versions", catalog.Entries[0].InitialTabId.Value);
    }

    [Fact]
    public void Parse_tabular_catalog_with_groups_and_default_property()
    {
        const string text = """
            module catalog demo
            default = soak

            | id   | title    | dashspec     | section  |
            | soak | Dev Soak | demo-soak.ds | overview |

            group extra
              title = "Extra"

              | id        | title     | dashspec          |
              | analytics | Analytics | demo-analytics.ds |
            end group
            """;

        var catalog = CatalogReader.Parse(text);

        Assert.Equal("demo", catalog.Id);
        Assert.Equal("soak", catalog.DefaultEntryId);
        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal("Dev Soak", catalog.Entries[0].Title);
        Assert.Equal("overview", catalog.Entries[0].InitialTabId!.Value);
        Assert.Equal("extra", catalog.Entries[1].GroupId!.Value);
        Assert.Single(catalog.Groups!.Value);
        Assert.Equal("Extra", catalog.Groups.Value[0].Title);
    }

    [Fact]
    public void Parse_rejects_duplicate_entry_id()
    {
        const string text = """
            module catalog x
            entry a dashspec "a.dashspec"
            entry a dashspec "b.dashspec"
            """;

        Assert.Throws<DashSpecParseException>(() => CatalogReader.Parse(text));
    }
}
