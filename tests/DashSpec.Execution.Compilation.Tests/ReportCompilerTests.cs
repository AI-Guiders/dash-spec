using DashSpec.Execution.Runtime;
using DashSpec.Execution.Compilation;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Xunit;

using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Compilation.Tests;

public sealed class ReportCompilerTests
{
    public ReportCompilerTests() => ExecutionDashSpecParser.EnsureModuleParsersRegistered();

    [Fact]
    public void ReportCompiler_implements_IReportCompiler()
    {
        IReportCompiler compiler = new ReportCompiler();
        Assert.NotNull(compiler);
    }

    [Fact]
    public void HotkeysModuleParser_normalizes_vim_keygesture_neovim_and_emacs_notations()
    {
        const string ds = """
            module hotkeys multi_notation_test
            namespace Test.Hotkeys

            notation = auto
            chord_root = "<C-k>"

            | id            | gesture    | notation    | scope   | action                  |
            | vim_chord     | <C-k>      | vim         | host    | command.chord_root      |
            | kg_chord      | Ctrl+K     | key-gesture | host    | command.chord_root      |
            | emacs_chord   | C-k        | emacs-kbd   | host    | command.chord_root      |
            | nvim_commit   | <CR>       | neovim-kbd  | command | command.commit          |
            | pointer_stack | <C-LMB>    | auto        | diagram | filter.add              |
            | pointer_kg    | Ctrl+LMB   | key-gesture | diagram | filter.add              |
            end hotkeys
            """;

        var doc = DashSpec.Modeling.Parse.Hotkeys.HotkeysModuleParser.parse(ds);
        Assert.Equal("multi_notation_test", doc.Id);
        Assert.Equal("Ctrl+K", doc.ChordRoot?.Value);
        Assert.Equal(6, doc.Bindings.Count);

        var vimChord = doc.Bindings[0];
        var kgChord = doc.Bindings[1];
        var emacsChord = doc.Bindings[2];
        Assert.Equal("Ctrl+K", vimChord.CanonicalGesture);
        Assert.Equal("Ctrl+K", kgChord.CanonicalGesture);
        Assert.Equal("Ctrl+K", emacsChord.CanonicalGesture);
        Assert.Equal("<C-k>", kgChord.CanonicalVimGesture);
        Assert.Equal(vimChord.NormalizedSequence.Steps[0], kgChord.NormalizedSequence.Steps[0]);
        Assert.Equal(vimChord.NormalizedSequence.Steps[0], emacsChord.NormalizedSequence.Steps[0]);

        var pointerStack = doc.Bindings[4];
        var pointerKg = doc.Bindings[5];
        Assert.Equal("Ctrl+LMB", pointerStack.CanonicalGesture);
        Assert.Equal("<C-LMB>", pointerKg.CanonicalVimGesture);
        Assert.Equal(pointerStack.NormalizedSequence.Steps[0], pointerKg.NormalizedSequence.Steps[0]);
    }

    [Fact]
    public void Stdlib_hotkeys_and_manual_compile_into_documentation_book()
    {
        var book = DashSpecDocGenerator.BuildDocumentationBook(null, "ru-RU");
        Assert.Contains(book.Pages, p => p.Id == "standard_hotkeys");
        Assert.True(book.GlobalSymbolIndex.ContainsKey("default_hotkeys"));
        Assert.True(book.GlobalSymbolIndex.ContainsKey("hotkey.diagram_filter_add"));
    }
}
