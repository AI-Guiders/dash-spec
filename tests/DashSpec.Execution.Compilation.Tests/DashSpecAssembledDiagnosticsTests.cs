using DashSpec.Execution.Compilation;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using FsharpDocument = DashSpec.Modeling.Parse.Document;
using Xunit;

using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Compilation.Tests;

/// <summary>
/// Assembled-view diagnostics (operator 2026-10-21: diagnostics must see what the runtime
/// sees). Tabs validate through each importing root (merge + graph validation, attributed via
/// the graph's own ModulePath/CardIds edges); roots validate with the merge ON; orphan tabs
/// keep the standalone composition; the corpus materializes once per member mtime revision.
/// </summary>
public sealed class DashSpecAssembledDiagnosticsTests
{
    const string CleanTabBody = """
        report
          card c as "C"
            show flow
              shared -> [toolbar] chrome.card.c
            end show flow

            diagram ref main test_table

            data flow
              test_rows_tz [rows] -> [rows] main
              shared -> [shared] main
            end data flow
          end card
        end report
        """;

    const string BrokenTabBody = """
        report
          card c as "C"
            diagram ref main test_table

            data flow
              test_rows_tz [rows] -> [rows] main
              shared -> [shared] main
            end data flow
          end card
        """;

    public DashSpecAssembledDiagnosticsTests() => ExecutionDashSpecParser.EnsureModuleParsersRegistered();

    [Fact]
    public void Tab_with_borrowed_filter_is_clean_assembled_where_standalone_would_fail()
    {
        var corpus = CreateCorpus(CleanTabBody);
        try
        {
            var tabPath = Path.Combine(corpus, "borrowed.ds");
            var diagnostics = DashSpecDiagnosticService.ValidateFile(tabPath, corpus);

            // The root declares `shared`; the tab only borrows it. Standalone would fail
            // ("more than one flow input" — the borrowed link falls into ModuleFlow); the
            // assembled view is the runtime truth and passes clean.
            Assert.DoesNotContain(diagnostics, d => d.Severity == DashSpecDiagnosticSeverity.Error);
        }
        finally
        {
            Cleanup(corpus);
        }
    }

    [Fact]
    public void Tab_with_conflicting_redeclaration_reports_the_merge_error()
    {
        var corpus = CreateCorpus("""
            report
              filters
                | id     | bind  | column   | label  | widget | ref | range |
                | shared | field | conflict | Shared | -      | -   | -     |
              end filters

              card c as "C"
                diagram ref main test_table

                data flow
                  test_rows_tz [rows] -> [rows] main
                  shared -> [shared] main
                end data flow
              end card
            end report
            """);
        try
        {
            var tabPath = Path.Combine(corpus, "borrowed.ds");
            var diagnostics = DashSpecDiagnosticService.ValidateFile(tabPath, corpus);

            // Same name as the root's filter, different column — the merge raises; the
            // error only exists in the assembled view and must reach the tab file.
            Assert.Contains(diagnostics, d =>
                d.Severity == DashSpecDiagnosticSeverity.Error
                && d.Message.Contains("redeclares filter 'shared'", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(corpus);
        }
    }

    [Fact]
    public void Root_diagnostics_fail_when_an_imported_tab_module_is_broken()
    {
        var corpus = CreateCorpus(BrokenTabBody);
        try
        {
            var rootPath = Path.Combine(corpus, "root.ds");
            var diagnostics = DashSpecDiagnosticService.ValidateFile(rootPath, corpus);

            // The tab module ends without `end report` — the runtime merge fails, so the
            // root's own diagnostics must fail too (the assembled view, merge ON).
            Assert.Contains(diagnostics, d => d.Severity == DashSpecDiagnosticSeverity.Error);
        }
        finally
        {
            Cleanup(corpus);
        }
    }

    [Fact]
    public void Orphan_tab_falls_back_to_standalone_with_a_warning()
    {
        var corpus = CreateCorpus(CleanTabBody);
        try
        {
            // Remove the root: nobody imports Test.Tabs anymore.
            File.Delete(Path.Combine(corpus, "root.ds"));

            var tabPath = Path.Combine(corpus, "borrowed.ds");
            var diagnostics = DashSpecDiagnosticService.ValidateFile(tabPath, corpus);

            // Standalone composition: the borrowed `shared` is not a local filter, so the
            // flow link falls into ModuleFlow and collides — the standalone error plus
            // the orphan warning (not imported by any root).
            Assert.Contains(diagnostics, d =>
                d.Severity == DashSpecDiagnosticSeverity.Error
                && d.Message.Contains("more than one flow input", StringComparison.Ordinal));
            Assert.Contains(diagnostics, d =>
                d.Severity == DashSpecDiagnosticSeverity.Warning
                && d.Message.Contains("not imported by any root", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(corpus);
        }
    }

    [Fact]
    public void Corpus_materializes_once_and_rematerializes_on_member_change()
    {
        var corpus = CreateCorpus(CleanTabBody);
        try
        {
            var first = DashSpecCompilerServicesHost.GetCorpus(corpus);
            var second = DashSpecCompilerServicesHost.GetCorpus(corpus);
            Assert.Same(first, second);

            // The assembled root compiles lazily and stays cached.
            var rootPath = Path.Combine(corpus, "root.ds");
            Assert.True(first.TryGetAssembledRoot(rootPath, out var documentA, out var errorA, out _));
            Assert.Null(errorA);
            Assert.True(first.TryGetAssembledRoot(rootPath, out var documentB, out var errorB, out _));
            Assert.Null(errorB);
            Assert.Same(documentA, documentB);

            // A member edit bumps an mtime — the next call materializes a fresh corpus.
            var tabPath = Path.Combine(corpus, "borrowed.ds");
            File.SetLastWriteTimeUtc(tabPath, DateTime.UtcNow.AddSeconds(5));
            var third = DashSpecCompilerServicesHost.GetCorpus(corpus);
            Assert.NotSame(first, third);

            // The fresh corpus reassembles with the same runtime truth.
            Assert.True(third.TryGetAssembledRoot(rootPath, out var documentC, out var errorC, out _));
            Assert.Null(errorC);
        }
        finally
        {
            Cleanup(corpus);
            DashSpecCompilerServicesHost.Invalidate(corpus);
        }
    }

    [Fact]
    public void Embedded_self_monitoring_report_compiles_and_builds_slot_plane()
    {
        var compiler = new ReportCompiler();
        var text = compiler.ReadEmbeddedUnit("stdlib/monitoring/self_monitoring.ds");
        Assert.False(string.IsNullOrWhiteSpace(text));

        var tempDir = Path.Combine(Path.GetTempPath(), "dspec-selfmon-" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var specFullPath = Path.Combine(tempDir, "self_monitoring.ds");
            var ok = PrecompiledReportBundle.TryGetOrCompile(
                text,
                specFullPath,
                tempDir,
                compiler,
                () => DashSpec.Compiler.Contract.DashSpecParseOptions.Default,
                out var compiled);

            Assert.True(ok);
            Assert.Equal("host-telemetry.runtime.toml", compiled.EntryRuntimePath);
            Assert.Equal(12, compiled.Document.Cards.Count);
            Assert.NotNull(compiled.SlotPlane);
            var boardSlots = compiled.SlotPlane.Boards.Values.First(s => s.Count > 0);
            Assert.Equal(12, boardSlots.Count);
            Assert.True(DashSpec.Execution.Runtime.CompiledRowTypes.Contains("TelemetryQueryRow"));
            Assert.True(DashSpec.Execution.Runtime.CompiledRowTypes.Contains("TelemetryLogRow"));

            var schema = new DashSpec.Abstractions.Data.RowTypeSchema("TelemetryMetricKpiRow", [
                new DashSpec.Abstractions.Data.RowFieldSchema("WorkingSetMb", DashSpec.Abstractions.Data.DashPrimitiveKind.Decimal, true),
                new DashSpec.Abstractions.Data.RowFieldSchema("ManagedHeapMb", DashSpec.Abstractions.Data.DashPrimitiveKind.Decimal, true),
            ]);
            var batch = DashSpec.Abstractions.Data.TypedRowBatch.Create(
                schema,
                [[DashSpec.Abstractions.Data.DashValue.FromDecimal(231.89999389648438m), DashSpec.Abstractions.Data.DashValue.FromDecimal(31.700000762939453m)]]);
            var rounded = DashSpec.Execution.Runtime.DashflowCardDataPipeline.ApplyTransforms(
                compiled.Document,
                compiled.Document.Cards.First(c => c.Id == "kpi_working_set"),
                batch);
            Assert.Equal(231.9m, rounded[0].Get("WorkingSetMb").AsDecimal());
            Assert.Equal(31.7m, rounded[0].Get("ManagedHeapMb").AsDecimal());

            Assert.True(compiled.Document.ResolvedDocumentation.TryGetValue("kpi_working_set", out var kpiDoc));
            Assert.Contains("Working Set", kpiDoc.Markdown);
            Assert.True(compiled.Document.ResolvedDocumentation.TryGetValue("TelemetryQueryRow", out var rowDoc));
            Assert.Contains("query_telemetry", rowDoc.Markdown);

            var stdlibCat = DashSpecDocGenerator.BuildStdlibCatalog("ru-RU");
            Assert.True(stdlibCat.SymbolIndex.TryGetValue("round", out var roundDoc));
            Assert.Contains("decimal", roundDoc.Markdown);
            Assert.Equal("runtime", DashSpecDocGenerator.TryGetSymbolTag(roundDoc, "locality"));
            Assert.True(stdlibCat.SymbolIndex.TryGetValue("unit.flow", out var flowUnitDoc));
            Assert.Equal("consumer", DashSpecDocGenerator.TryGetSymbolTag(flowUnitDoc, "role"));
            Assert.NotEmpty(flowUnitDoc.Children);
            Assert.True(stdlibCat.SymbolIndex.TryGetValue("DateTime", out var dtTypeDoc));
            Assert.Equal("type", dtTypeDoc.Kind);
            var md = DashSpecDocGenerator.RenderMarkdown(stdlibCat);
            Assert.Contains("module transform round", md);

            var book = DashSpecDocGenerator.BuildDocumentationBook(tempDir, "ru-RU");
            Assert.Equal("dashspec_manual", book.Id);
            Assert.True(book.Pages.Count >= 7);
            var unitsPage = book.Pages.First(p => p.Id == "language_units");
            Assert.Equal(3, unitsPage.SymbolSections.Count); // declaration, consumer, root
            var transformsPage = book.Pages.First(p => p.Id == "dataflow_transforms");
            Assert.Equal(2, transformsPage.SymbolSections.Count); // database, runtime
            var rulesPage = book.Pages.First(p => p.Id == "compiler_rules");
            Assert.Equal(3, rulesPage.SymbolSections.Count); // error, warning, info
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    string CreateCorpus(string tabBody)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dspec-assembled-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "test.toml"), "# stub manifest\n");
        File.WriteAllText(Path.Combine(dir, "types.ds"), """
            module types test_types
            namespace Test.Types

            type TestRow
              optional string name
              optional int count
            end type
            """);
        File.WriteAllText(Path.Combine(dir, "flow.ds"), """
            module flow test_flow
            namespace Test.Flows

            import types from Test.Types

            source test_src {
              use provider infer
              from view test.v_rows
              ports
                default output raw
                output stream raw: TestRow
              end ports
            }

            transformer test_rows_tz {
              ports
                default input raw
                default output rows
                input stream raw: TestRow
                output stream rows: TestRow
              end ports
              transform use to_zone { zone = UTC+3 }
            }

            test_src [raw] -> [raw] test_rows_tz

            end flow
            """);
        File.WriteAllText(Path.Combine(dir, "diagram.ds"), """
            module diagram test_table
            namespace Test.Diagrams

            table
              columns
                | id   | format | label | sorting |
                | name | -      | Name  | ASC     |
              end columns
            end table
            """);
        File.WriteAllText(Path.Combine(dir, "borrowed.ds"), $"""
            module section borrowed
            namespace Test.Tabs

            import types from Test.Types
            import flows from Test.Flows
            import diagrams from Test.Diagrams

            runtime
              manifest = "test.toml"
            end runtime

            {tabBody}
            end section
            """);
        File.WriteAllText(Path.Combine(dir, "root.ds"), """
            module dashboard test_root
            namespace Test.Root

            import sections from Test.Tabs

            runtime
              manifest = "test.toml"
            end runtime

            report
              title = "Test Root"

              filters
                | id     | bind  | column | label  | widget | ref | range |
                | shared | field | shared | Shared | -      | -   | -     |
              end filters

              section borrowed as "Borrowed"
            end report
            end dashboard
            """);

        return dir;
    }

    static void Cleanup(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // ignore — temp dirs
        }
    }
}
