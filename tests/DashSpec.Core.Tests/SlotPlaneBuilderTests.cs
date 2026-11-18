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

        // The dump is readable for the eyes and shows the recursive Inner container tree.
        var dump = SlotPlaneDump.Dump(plane);
        Assert.Contains("boards:", dump);
        Assert.Contains("interiors:", dump);
        Assert.Contains("-> [1x12](", dump);
        Assert.All(plane.Boards.Values.SelectMany(s => s), cardSlot =>
        {
            Assert.NotNull(cardSlot.Inner);
            Assert.NotEmpty(cardSlot.Inner!.Value.Slots);
        });
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
            import presentations from DashSpec.Std

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

    [Fact]
    public void Card_filter_chrome_with_manual_apply_assembles_apply_chrome_node_in_slot_plane()
    {
        var text = """
            module dashboard chrome_probe
            namespace Demo.Chrome

            import diagrams from Demo.Diagrams
            import types from Demo.Types
            import flows from Demo.Flows as demo_report_flow
            import presentations from DashSpec.Std

            runtime
                manifest = "demo.toml"
            end runtime

            report
                title = "Chrome Probe"
                defaults
                    filter.usage_date.range = -7d..today
                end defaults
                filter usage_date
                    bind date
                        column = usage_date
                    end bind
                    show
                        label = "Date"
                    end show
                end filter
                filter app_name
                    bind field
                        column = demo.v_daily_active_events.app_name
                    end bind
                    show
                        label = "App"
                        apply = manual
                        widget = combobox
                    end show
                end filter

                card peak as "Peak"
                    show flow
                        usage_date -> [toolbar] chrome.card.peak
                        app_name -> [toolbar] chrome.card.peak
                    end show flow
                    filters
                        apply = manual
                        layout
                            [ usage_date:2 apply:1 app_name:1 ]
                        end layout
                    end filters
                    data flow
                        daily_peak_tz [rows] -> [rows] __diagram__
                    end data flow
                    diagram demo_peak_gauge
                        bind
                            usage_date, app_name
                        end bind
                end card
            end report
            end dashboard
            """;

        var document = new ReportCompiler()
            .Compile(text, DemoDir, DashSpecParseOptions.Default)
            .Document;
        var library = SpecLibraryComposer.Load(DemoSoakPath, document.DiagramLibraryPath?.Value, DemoDir, document);
        var plane = SlotPlaneBuilder.Build(document, library);

        var chromeSlots = plane.CardFilterChrome["peak"];
        Assert.Equal(3, chromeSlots.Count);
        Assert.Equal(["usage_date", "apply", "app_name"], chromeSlots.Select(s => s.Node.Id).ToArray());
        var applySlot = chromeSlots.Single(s => s.Node.Id == "apply");
        Assert.Equal(WiringNodeKind.Chrome, applySlot.Node.Kind);
        Assert.Equal(PlacementProvenance.RecordedDefault, applySlot.Provenance);
    }

    [Fact]
    public void Layout_module_supports_nested_vstack_hstack_and_explicit_wide_mid_narrow_boards()
    {
        var layoutText = """
            module layout adaptive_probe
            namespace Demo.Adaptive
            scope section

            wide
                hstack
                    vstack:1
                        c1
                        c2
                    end vstack
                    vstack:3
                        c3
                        c4
                    end vstack
                end hstack
            end wide

            mid
                vstack
                    hstack
                        c1
                        c2
                    end hstack
                    hstack
                        c3
                        c4
                    end hstack
                end vstack
            end mid

            narrow
                vstack
                    c1
                    c2
                    c3
                    c4
                end vstack
            end narrow
            """;

        var board = DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(layoutText);
        Assert.Equal(LayoutScope.Tab, board.ModuleScope?.Value);

        // Wide: hstack of two vstacks synthesizes 2 NestRows (__stack_1, __stack_2) + 1 CardRow [ __stack_1:1 __stack_2:3 ].
        Assert.Equal(3, board.Entries.Count);
        Assert.Equal(["__stack_1:1", "__stack_2:3"], board.Rows.Single().ToArray());

        // Mid: vstack of two hstacks -> 2 CardRows: [ c1 c2 ] and [ c3 c4 ].
        Assert.NotNull(board.MidEntries);
        var midRows = board.MidEntries!.Value
            .OfType<LayoutBoardEntry.CardRow>()
            .Select(r => r.Item.ToArray())
            .ToArray();
        Assert.Equal(2, midRows.Length);
        Assert.Equal(["c1", "c2"], midRows[0]);
        Assert.Equal(["c3", "c4"], midRows[1]);

        // Narrow: vstack of 4 items -> 4 CardRows of 1 cell each.
        Assert.NotNull(board.NarrowEntries);
        var narrowRows = board.NarrowEntries!.Value
            .OfType<LayoutBoardEntry.CardRow>()
            .Select(r => r.Item.ToArray())
            .ToArray();
        Assert.Equal(4, narrowRows.Length);
        Assert.Equal(["c1"], narrowRows[0]);
        Assert.Equal(["c4"], narrowRows[3]);
    }

    [Fact]
    public void Card_interior_layout_supports_nested_vstack_and_hstack_with_explicit_mid_override()
    {
        var text = """
            module dashboard stack_card_probe
            namespace Demo.StackCard

            import diagrams from Demo.Diagrams
            import types from Demo.Types
            import flows from Demo.Flows as demo_report_flow
            import presentations from DashSpec.Std

            runtime
                manifest = "demo.toml"
            end runtime

            report
                title = "Stack Card Probe"
                defaults
                    filter.usage_date.range = -7d..today
                end defaults
                filter usage_date
                    bind date
                        column = usage_date
                    end bind
                    show
                        label = "Date"
                    end show
                end filter
                filter app_name
                    bind field
                        column = demo.v_daily_active_events.app_name
                    end bind
                    show
                        label = "App"
                        widget = combobox
                    end show
                end filter

                card peak as "Peak"
                    show flow
                        usage_date -> [toolbar] chrome.card.peak
                        app_name -> [toolbar] chrome.card.peak
                    end show flow
                    layout
                        wide
                            vstack
                                hstack
                                    usage_date
                                    app_name
                                end hstack
                                diagram
                            end vstack
                        end wide
                        mid
                            vstack
                                usage_date
                                app_name
                                diagram
                            end vstack
                        end mid
                    end layout
                    data flow
                        daily_peak_tz [rows] -> [rows] __diagram__
                    end data flow
                    diagram demo_peak_gauge
                        bind
                            usage_date, app_name
                        end bind
                end card
            end report
            end dashboard
            """;

        var document = new ReportCompiler()
            .Compile(text, DemoDir, DashSpecParseOptions.Default)
            .Document;
        var library = SpecLibraryComposer.Load(DemoSoakPath, document.DiagramLibraryPath?.Value, DemoDir, document);
        var plane = SlotPlaneBuilder.Build(document, library);

        var interior = plane.CardInteriors["peak"];
        Assert.Equal(3, interior.Count);

        var usageSlot = interior.Single(s => s.Node.Id == "usage_date");
        var appSlot = interior.Single(s => s.Node.Id == "app_name");
        var diagSlot = interior.Single(s => s.Node.Id == "__diagram__");

        // Wide: usage_date and app_name share row 1 (span 6 each), diagram is on row 2 (span 12).
        Assert.Equal(new PlacementDefinition(1, 1, 6), usageSlot.Placement.Wide);
        Assert.Equal(new PlacementDefinition(1, 7, 6), appSlot.Placement.Wide);
        Assert.Equal(new PlacementDefinition(2, 1, 12), diagSlot.Placement.Wide);

        // Mid: explicit vstack places usage_date on row 1, app_name on row 2, diagram on row 3 (each full mid width = 6).
        Assert.Equal(new PlacementDefinition(1, 1, 6), usageSlot.Placement.Mid);
        Assert.Equal(new PlacementDefinition(2, 1, 6), appSlot.Placement.Mid);
        Assert.Equal(new PlacementDefinition(3, 1, 6), diagSlot.Placement.Mid);
    }

    [Fact]
    public void Layout_supports_all_five_universal_surface_forms_and_named_card_presets_with_role_tokens()
    {
        // 1. Module layout written with pipe table (wide), property assignments (mid), and bare lines / inline colon stacks (narrow).
        var layoutFormsText = """
            module layout universal_forms
            namespace Demo.Universal
            scope section

            wide
                | c1:4 | c2:8 |
                | c3   | c4   |
            end wide

            mid
                hstack = [ c1, c2 ]
                vstack = [ c3, c4 ]
            end mid

            narrow
                hstack: c1 c2
                c3
                c4
            end narrow
            """;

        var board = DashSpec.Modeling.Parse.Layout.LayoutModuleParser.parseLayoutFile(layoutFormsText);
        Assert.Equal(2, board.RowCount);
        Assert.Equal(["c1:4", "c2:8"], board.Rows[0].ToArray());
        Assert.Equal(["c3", "c4"], board.Rows[1].ToArray());

        var midRows = board.MidEntries!.Value.OfType<LayoutBoardEntry.CardRow>().Select(r => r.Item.ToArray()).ToArray();
        Assert.Equal(3, midRows.Length);
        Assert.Equal(["c1", "c2"], midRows[0]);
        Assert.Equal(["c3"], midRows[1]);
        Assert.Equal(["c4"], midRows[2]);

        var narrowRows = board.NarrowEntries!.Value.OfType<LayoutBoardEntry.CardRow>().Select(r => r.Item.ToArray()).ToArray();
        Assert.Equal(3, narrowRows.Length);
        Assert.Equal(["c1", "c2"], narrowRows[0]);
        Assert.Equal(["c3"], narrowRows[1]);
        Assert.Equal(["c4"], narrowRows[2]);

        // 2. Card layout preset (`scope card` with `@filters` and `@primary`) + PropertyBlock written as a pipe table!
        var tempDir = Path.Combine(Path.GetTempPath(), "dashspec-universal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "card-preset.ds"), """
                module layout card_filters_top
                namespace Custom.CardLayouts
                scope card

                @filters
                @primary
                """);

            var dashText = """
                module dashboard preset_probe
                namespace Custom.Dashboard

                import layouts from Custom.CardLayouts
                import types from Fixtures.QueryRowTypes

                report
                    title = "Preset & Table PropertyBlock Probe"
                    defaults
                        filter.usage_date.range = -7d..today
                    end defaults
                    filter usage_date
                        bind date
                            column = usage_date
                        end bind
                        show
                            label = "Date"
                        end show
                    end filter
                    filter app_name
                        bind field
                            column = a
                        end bind
                        show
                            label = "App"
                            widget = combobox
                        end show
                    end filter

                    card peak as "Peak"
                        show flow
                            usage_date -> [toolbar] chrome.card.peak
                            app_name -> [toolbar] chrome.card.peak
                        end show flow
                        layout = card_filters_top
                        diagram bar
                            | key | value      |
                            | x   | usage_date |
                            | y   | value      |
                        end bar
                        presentation
                            | colors  | height |
                            | #4e79a7 | 320    |
                        end presentation
                        data flow { fixture_src [rows] -> [rows] __diagram__ }
                        bind
                            usage_date, app_name
                        end bind
                    end card
                end report
                end dashboard
                """;

            var doc = DashSpecTestRowTypes.ParseDashboard(dashText, tempDir);
            var card = doc.Cards.Single();

            // Verify pipe-table PropertyBlock on `diagram` and `presentation`:
            Assert.Equal("usage_date", card.Diagram.Properties["x"]);
            Assert.Equal("value", card.Diagram.Properties["y"]);
            Assert.Equal("#4e79a7", card.Presentation!.Value.Properties["colors"]);
            Assert.Equal("320", card.Presentation!.Value.Properties["height"]);

            // Verify `@filters` expanded to `[ usage_date app_name ]` on row 1 and `@primary` expanded to `[ diagram ]` on row 2:
            var placements = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
            Assert.Equal(new PlacementDefinition(1, 1, 6), placements["usage_date"]);
            Assert.Equal(new PlacementDefinition(1, 7, 6), placements["app_name"]);
            Assert.Equal(new PlacementDefinition(2, 1, 12), placements["__diagram__"]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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
