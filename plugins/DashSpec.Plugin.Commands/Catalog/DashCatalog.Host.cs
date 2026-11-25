#nullable enable

using System.Reflection;
using AIGuiders.Platform.Authoring.Command.Catalog;
using DashSpec.Execution.Parsing;
using DashSpec.Modeling.Parse;
using DashSpec.Modeling.Parse.Document;
using DashSpec.Modeling.Parse.Hotkeys;
using DashSpec.Modeling.Parse.Syntax;
using DashSpec.Surface.Blazor.Commands;
using Microsoft.FSharp.Core;

namespace DashSpec.Generated;

/// <summary>
/// Embedded <c>dash.commands.ds</c> catalog parsed via DashSpec's native AST + Tier 2 <see cref="Construct"/>.
/// Replaces the external <c>dash.catalog.gdl</c> / <c>gdlc emit</c> codegen while preserving the exact runtime shape.
/// </summary>
public static class DashCatalog
{
    private const string EmbeddedResourceName = "DashSpec.Plugin.Commands.Resources.dash.commands.ds";

    public const string Planet = "dash";

    public const string CardViewCatalogCommand = "card.view";

    public const string MatrixValuesToggleCatalogCommand = "card.matrix.values.toggle";

    public const string MatrixAxisXToggleCatalogCommand = "card.matrix.axis_x.toggle";

    public const string MatrixAxisYToggleCatalogCommand = "card.matrix.axis_y.toggle";

    public static readonly string[] FederationSurfaces =
    [
        "slash.bar",
        "palette",
        "console.filter",
        "ccl.filter",
    ];

    public static readonly IReadOnlyDictionary<string, string> WireCommandIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["filter.date"] = "dash.filter.date",
            ["filter.field"] = "dash.filter.field",
            ["report.select"] = "dash.report.select",
            ["page.select"] = "dash.page.select",
            ["card.view"] = "dash.card.view",
            ["card.matrix.values.toggle"] = "dash.card.matrix.values.toggle",
            ["card.matrix.axis_x.toggle"] = "dash.card.matrix.axis_x.toggle",
            ["card.matrix.axis_y.toggle"] = "dash.card.matrix.axis_y.toggle",
            ["host.show"] = "dash.host.show",
        };

    public static readonly string[] ExposedMcpCommands =
    [
        "dash.filter.date",
        "dash.host.show",
    ];

    public static readonly IReadOnlyDictionary<string, string> PhraseSlotLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["filter"] = "Имя фильтра на toolbar",
        };

    public static readonly CatalogPhraseSlotEmit[] PhraseSlotCommands =
    [
        new("filter.date", "dash.filter.date", "select filter", ["filter"]),
        new("filter.field", "dash.filter.field", "select filter", ["filter"]),
        new("report.select", "dash.report.select", "select report", ["report"]),
        new("page.select", "dash.page.select", "select page", ["page"]),
        new("card.view", "dash.card.view", "view", ["card", "view"]),
        new("card.matrix.values.toggle", "dash.card.matrix.values.toggle", "toggle matrix values", ["card"]),
        new("card.matrix.axis_x.toggle", "dash.card.matrix.axis_x.toggle", "toggle matrix axis-x", ["card"]),
        new("card.matrix.axis_y.toggle", "dash.card.matrix.axis_y.toggle", "toggle matrix axis-y", ["card"]),
        new("host.show", "dash.host.show", "show host", ["surface"]),
    ];

    public static CatalogPhraseSlotIndex PhraseSlots =>
        CatalogPhraseSlotIndex.FromEmitted(PhraseSlotCommands, PhraseSlotLabels);

    public static CatalogDocument Document { get; } = LoadFromEmbeddedDs();

    public static HotkeysDocument Hotkeys { get; } = LoadStdlibHotkeys();

    public static IReadOnlyList<CatalogPhrase> Phrases => Document.Phrases;

    public static IReadOnlyList<CatalogBindingRow> Bindings => Document.Bindings;

    public static string Summary => CatalogSummary.Format(Document);

    public static string CardViewWireCommandId => WireCommandIds[CardViewCatalogCommand];

    public static CatalogPhraseSlotCommand CardViewPhrase =>
        PhraseSlots.Commands.First(command =>
            command.CatalogCommand.Equals(CardViewCatalogCommand, StringComparison.OrdinalIgnoreCase));

    public static CatalogPhraseSlotCommand MatrixValuesTogglePhrase =>
        PhraseSlots.Commands.First(command =>
            command.CatalogCommand.Equals(MatrixValuesToggleCatalogCommand, StringComparison.OrdinalIgnoreCase));

    public static CatalogPhraseSlotCommand MatrixAxisXTogglePhrase =>
        PhraseSlots.Commands.First(command =>
            command.CatalogCommand.Equals(MatrixAxisXToggleCatalogCommand, StringComparison.OrdinalIgnoreCase));

    public static CatalogPhraseSlotCommand MatrixAxisYTogglePhrase =>
        PhraseSlots.Commands.First(command =>
            command.CatalogCommand.Equals(MatrixAxisYToggleCatalogCommand, StringComparison.OrdinalIgnoreCase));

    static DashCatalog()
    {
        DashboardCatalogFlavor.ValidateAtLoad(Document);
    }

    public static string ReadEmbeddedCommandsText()
    {
        var assembly = typeof(DashCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded commands resource '{EmbeddedResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static HotkeysDocument LoadStdlibHotkeys()
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        var rawOpt = UnitSource.tryReadRaw("stdlib/hotkeys/hotkeys.ds");
        if (FSharpOption<string>.get_IsSome(rawOpt) && !string.IsNullOrWhiteSpace(rawOpt.Value))
        {
            return HotkeysModuleParser.parse(rawOpt.Value);
        }

        return HotkeysModuleParser.parse(
            """
            module hotkeys default_hotkeys
            namespace DashSpec.Std
            notation = auto
            | id              | gesture | scope   | action                  |
            | chord_root      | <C-k>   | host    | command.chord_root      |
            | suggest_dismiss | <C-.>   | command | command.suggest_dismiss |
            end hotkeys
            """);
    }

    private static CatalogDocument LoadFromEmbeddedDs()
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        var hotkeys = LoadStdlibHotkeys();
        var text = ReadEmbeddedCommandsText();
        var tree = DashSpecSurfaceSyntax.parse(text);
        var env = UnitGrammar.tryUnwrapAny(tree.Root)?.Value
            ?? throw new InvalidOperationException("Embedded dash.commands.ds must declare 'module commands <id>'.");

        var phrases = new List<CatalogPhrase>();
        var bindings = new List<CatalogBindingRow>();

        foreach (var hk in hotkeys.Bindings)
        {
            var role = hk.Action switch
            {
                "command.chord_root" or "chord-root" => "chord-root",
                "command.suggest_dismiss" or "suggest-dismiss" => "suggest-dismiss",
                _ => hk.Id,
            };
            bindings.Add(new CatalogBindingRow(hk.CanonicalGesture, hk.Action, role));
        }

        foreach (var block in env.View.Blocks)
        {
            var blockName = block.Tokens.FirstOrDefault()?.Text ?? string.Empty;
            if (blockName.Equals("phrases", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var row in ReadTableRows(block.Members))
                {
                    if (row.TryGetValue("name", out var name) && row.TryGetValue("phrase", out var phrase))
                    {
                        phrases.Add(new CatalogPhrase(name, phrase));
                    }
                }
            }
            else if (blockName.Equals("bindings", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var row in ReadTableRows(block.Members))
                {
                    if (row.TryGetValue("gesture", out var gesture) && row.TryGetValue("role", out var role))
                    {
                        var normRes = HotkeyGestureNormalizer.tryParseGesture(HotkeyNotationKind.Auto, gesture);
                        var canonical = normRes.IsOk
                            ? HotkeyGestureNormalizer.toCanonicalKeyGesture(normRes.ResultValue)
                            : gesture;
                        var cmd = row.GetValueOrDefault("command", "—");
                        var existingIdx = bindings.FindIndex(b => string.Equals(b.Role, role, StringComparison.OrdinalIgnoreCase));
                        var rowEntry = new CatalogBindingRow(canonical, cmd == "-" ? "—" : cmd, role);
                        if (existingIdx >= 0)
                        {
                            bindings[existingIdx] = rowEntry;
                        }
                        else
                        {
                            bindings.Add(rowEntry);
                        }
                    }
                }
            }
        }

        var resolvedChordRoot = FSharpOption<string>.get_IsSome(hotkeys.ChordRoot)
            ? hotkeys.ChordRoot.Value
            : "Ctrl+K";

        return new CatalogDocument
        {
            Planet = Planet,
            Defaults = new CatalogDefaults
            {
                VariableKind = "phrase-slot",
                CommandScope = "dashboard",
                CommandSurfaces = FederationSurfaces,
                GrammarKeyboardBinding = "keyboard-key-gesture",
                GrammarKeyboardMelody = "keyboard-key-gesture",
                BindingChordRoot = resolvedChordRoot,
                CommandFlavor = "console",
            },
            Channels =
            [
                new CatalogChannel
                {
                    Surface = "slash",
                    Sub = "bar",
                    PlanetId = "toolbar-slash",
                    CommandGrammar = "command-slash",
                    ArgumentGrammar = "argument-slash",
                },
                new CatalogChannel
                {
                    Surface = "console",
                    Sub = "filter",
                    PlanetId = "filter-bar",
                    CommandGrammar = "command-console",
                    ArgumentGrammar = "argument-kv",
                },
                new CatalogChannel
                {
                    Surface = "ccl",
                    Sub = "filter",
                    PlanetId = "filter-ccl",
                    CommandGrammar = "command-console",
                    ArgumentGrammar = "argument-kv",
                },
                new CatalogChannel
                {
                    Surface = "palette",
                    PlanetId = "command-palette",
                },
            ],
            Phrases = phrases,
            Bindings = bindings,
        };
    }

    private static List<Dictionary<string, string>> ReadTableRows(DashSpecAstNode[] members)
    {
        var tableOpt = Construct.tryFromAstTable(members);
        if (!FSharpOption<Construct.ConstructForm>.get_IsSome(tableOpt) ||
            tableOpt.Value is not Construct.ConstructForm.TableForm { rows: { Length: > 1 } rawRows })
        {
            return [];
        }

        var headers = rawRows[0].Select(h => h.Trim()).ToArray();
        var list = new List<Dictionary<string, string>>();
        foreach (var cells in rawRows.Skip(1))
        {
            if (cells.All(c => c.All(ch => ch is '-' or ':')))
            {
                continue;
            }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < Math.Min(headers.Length, cells.Length); i++)
            {
                var k = headers[i];
                var v = Construct.unquote(cells[i].Trim());
                if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v))
                {
                    map[k] = v;
                }
            }

            if (map.Count > 0)
            {
                list.Add(map);
            }
        }

        return list;
    }
}
