using DashSpec.Core.Parsing;
using DashSpec.Execution.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// A `.ds` unit always carries a module header (`module &lt;kind&gt; &lt;id&gt;`, `namespace`, `import`), and every
/// parser reads only the body — so the header is blanked once at the bridge entry
/// (<c>ModuleParseRegistration.UnitText</c>). This guards the class of bug where a bridge fed the raw unit to a
/// parser that then read `namespace` as a statement and failed.
/// </summary>
public sealed class UnitBridgeHeaderTests
{
    public UnitBridgeHeaderTests() => DashSpec.Execution.Parsing.DashSpecParser.EnsureModuleParsersRegistered();

    [Fact]
    public void Layout_unit_from_the_demo_parses()
    {
        var board = LayoutModuleParser.ParseLayoutFile(ReadUnit("samples/demo/layouts/host-topbar.ds"));

        Assert.NotNull(board);
    }

    [Fact]
    public void Palette_unit_from_the_demo_parses()
    {
        var (id, properties) = PaletteModuleParser.ParsePaletteFile(ReadUnit("samples/demo/palettes/demo-apps.ds"));

        Assert.Equal("demo_apps", id);
        Assert.Equal("#94a3b8", properties["default"]);
    }

    [Fact]
    public void Diagram_unit_from_the_demo_parses()
    {
        var (id, fragment) = DiagramModuleParser.ParseDiagramFileWithId(
            ReadUnit("samples/demo/diagrams/dau-bar.ds"),
            Repo("samples/demo/diagrams"));

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.NotNull(fragment.Diagram);
    }

    [Fact]
    public void Tooltip_unit_from_the_demo_parses()
    {
        var (id, definition) = TooltipModuleParser.ParseTooltipFileWithId(
            ReadUnit("samples/demo/tooltips/peak-apps.ds"));

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.NotNull(definition);
    }

    [Fact]
    public void Catalog_unit_from_the_demo_parses()
    {
        var catalog = CatalogParser.Parse(ReadUnit("samples/demo/demo-catalog.ds"));

        Assert.NotEmpty(catalog.Entries);
    }

    [Fact]
    public void Presentation_unit_with_header_parses()
    {
        var (id, block) = PresentationModuleParser.ParsePresentationFileWithId(
            ReadUnit("src/DashSpec.Core/stdlib/presentation/bar_stacked_360.ds"),
            null);

        Assert.Equal("bar_stacked_360", id);
        Assert.Equal("360", block.Properties["height"]);
    }

    [Fact]
    public void Host_unit_from_the_demo_parses()
    {
        var host = HostModuleParser.Parse(ReadUnit("samples/demo/demo.ds"), Repo("samples/demo"));

        Assert.NotNull(host);
    }

    private static string ReadUnit(string relativePath) => File.ReadAllText(Repo(relativePath));

    private static string Repo(string relativePath) =>
        Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
            relativePath);
}
