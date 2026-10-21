using DashSpec.Execution.Compilation;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.Layout;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Xunit;

using DashSpec.Compiler.Contract;

namespace DashSpec.Core.Tests;

/// <summary>
/// Assembled slot plane ([ADR-0111] P0): the correspondence — compiled thing ↔ its visual entry —
/// assembled once over the demo corpus (tabbed, inline tabs) and a minimal tabless document;
/// the render is to read it and place (P1 switches the consumers).
/// </summary>
public sealed class SlotPlaneBuilderTests
{
    static readonly string DemoDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "samples", "demo"));

    static readonly string DemoSoakPath = Path.Combine(DemoDir, "demo-soak.ds");

    public SlotPlaneBuilderTests() => ExecutionDashSpecParser.EnsureModuleParsersRegistered();

    [Fact]
    public void Demo_corpus_assembles_tabbed_routes_toolbar_and_interiors()
    {
        Assert.True(File.Exists(DemoSoakPath), $"Missing {DemoSoakPath}");
        var document = new ReportCompiler()
            .Compile(File.ReadAllText(DemoSoakPath), DemoDir, DashSpecParseOptions.Default)
            .Document;

        // The living diagram library: the imports merged by the composer ([ADR-0098]);
        // the @diagramlibrary TOML is the legacy tail, the demo resolves through the imports.
        var library = SpecLibraryComposer.Load(DemoSoakPath, document.DiagramLibraryPath?.Value, DemoDir, document);
        var plane = SlotPlaneBuilder.Build(document, library);

        // The initial route: the first tab, recorded.
        Assert.NotEqual("", plane.InitialRoute.TabId);
        Assert.Equal("", plane.InitialRoute.PageId);

        // One route per top-level tab (no pages); each board slice holds the tab's cards
        // and every slot corresponds to a Card node of the document graph.
        Assert.NotEmpty(plane.Boards);
        foreach (var (route, board) in plane.Boards)
        {
            Assert.NotNull(route.TabId?.Value);
            Assert.Null(route.PageId);
            Assert.NotEmpty(board);
            Assert.All(board, slot => Assert.Equal(WiringNodeKind.Card, slot.Node.Kind));
        }

        // The toolbar slice: declared filters correspond to Filter nodes.
        Assert.NotEmpty(plane.Toolbars);
        Assert.All(
            plane.Toolbars.Values.SelectMany(slots => slots),
            slot => Assert.Equal(WiringNodeKind.Filter, slot.Node.Kind));

        // Interiors: every card has a slice; card-scoped slot nodes go by their bare
        // names (the wiring convention — the scope lives on the edges).
        Assert.Equal(document.Cards.Count, plane.CardInteriors.Count);
        Assert.All(plane.CardInteriors, pair =>
        {
            Assert.Contains(pair.Value, slot =>
                slot.Node.Kind == WiringNodeKind.Slot
                && slot.Scope == LayoutSlotScope.CardInterior);
        });

        // The dump is readable for the eyes.
        var dump = SlotPlaneDump.Dump(plane);
        Assert.Contains("boards:", dump);
        Assert.Contains("interiors:", dump);
    }

    [Fact]
    public void Tabless_document_assembles_the_implicit_board_with_recorded_defaults()
    {
        var text = """
            module dashboard tabless_probe
            namespace Demo.Tabless

            import diagrams from Demo.Diagrams
            import types from Demo.Types
            import flows from Demo.Flows as demo_report_flow

            runtime
                manifest = "demo.toml"
            end runtime

            report
                title = "Tabless Probe"

                card peak_gauge_probe as "Gauge"
                    data flow
                        daily_peak_tz [rows] -> [rows] __diagram__
                    end data flow
                    diagram demo_peak_gauge
                end card
            end report
            end dashboard
            """;

        var document = new ReportCompiler()
            .Compile(text, DemoDir, DashSpecParseOptions.Default)
            .Document;
        var library = SpecLibraryComposer.Load(DemoSoakPath, document.DiagramLibraryPath?.Value, DemoDir, document);
        var plane = SlotPlaneBuilder.Build(document, library);

        // Tabless document: the implicit route, recorded.
        Assert.Equal("", plane.InitialRoute.TabId);

        // The implicit board: one route over all cards — the family default recorded.
        var implicitRoute = Assert.Single(plane.Boards.Keys, route =>
            route.TabId is null && route.PageId is null);
        var board = plane.Boards[implicitRoute];
        var slot = Assert.Single(board);
        Assert.Equal("peak_gauge_probe", slot.Node.Id);
        Assert.Equal(WiringNodeKind.Card, slot.Node.Kind);
        Assert.Equal(PlacementProvenance.RecordedDefault, slot.Provenance);
    }
}

file static class AssertSingleExtensions
{
    public static T Single<T>(this IReadOnlyCollection<T> source, Func<T, bool> predicate, string? message = null)
    {
        var matches = source.Where(predicate).ToList();
        Assert.True(matches.Count == 1, message ?? $"Expected exactly one match, found {matches.Count}.");
        return matches[0];
    }
}
