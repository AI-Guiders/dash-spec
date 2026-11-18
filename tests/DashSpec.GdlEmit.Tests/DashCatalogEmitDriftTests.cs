#nullable enable

using DashSpec.Generated;
using Xunit;

namespace DashSpec.GdlEmit.Tests;

/// <summary>Verifies that the embedded <c>dash.commands.ds</c> catalog parses cleanly via DashSpec's native AST + Tier 2 Construct.</summary>
public sealed class DashCatalogEmitDriftTests
{
    [Fact]
    public void Embedded_dash_commands_ds_loads_phrases_bindings_and_aliases()
    {
        var text = DashCatalog.ReadEmbeddedCommandsText();
        Assert.Contains("module commands dash_commands", text);

        Assert.Equal(8, DashCatalog.Phrases.Count);
        Assert.Equal(2, DashCatalog.Bindings.Count);
        Assert.Equal("view", DashCatalog.CardViewPhrase.LiteralPrefix);
        Assert.Equal("toggle matrix values", DashCatalog.MatrixValuesTogglePhrase.LiteralPrefix);
        Assert.Equal("toggle matrix axis-x", DashCatalog.MatrixAxisXTogglePhrase.LiteralPrefix);
        Assert.Equal("toggle matrix axis-y", DashCatalog.MatrixAxisYTogglePhrase.LiteralPrefix);

        var aliases = DashSpec.Modeling.Parse.Toolbar.CommandAliasesParser.parseModule(text);
        Assert.Equal("select filter {filter}", aliases["filter.date"]);
        Assert.Equal("view {card} {view}", aliases["card.view"]);
    }
}
