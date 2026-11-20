using DashSpec.Modeling.Parse;
using DashSpec.Modeling.Parse.Document;
using DashSpec.Modeling.Parse.Syntax;
using Microsoft.FSharp.Core;

namespace DashSpec.Execution.Compilation;

public sealed record DashSpecUnitDeclaration(string Kind, string Id, string Path, int Line)
{
    public string Namespace { get; init; } = "";
}

/// <summary>
/// Workspace unit index for LSP completion, go-to-definition and find-usages:
/// built over <see cref="UnitIndexModule.build"/> (<c>.dsproj</c> membership + embedded <c>stdlib/</c>)
/// and typed <see cref="UnitGrammar.tryUnwrapAny"/> AST envelopes.
/// </summary>
public sealed class DashSpecWorkspaceIndex
{
    private readonly Dictionary<string, string> _diagrams =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _presentations =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<DashSpecUnitDeclaration> _units = [];

    private readonly Dictionary<string, DashSpecUnitDeclaration> _unitsById =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<DashSpecUnitDeclaration>> _unitsByNamespace =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string Path, int Line, int Column)> _rowTypes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string Text, ParseTree Tree)> _unitSources =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Diagrams => _diagrams;

    public IReadOnlyDictionary<string, string> Presentations => _presentations;

    public IReadOnlyList<DashSpecUnitDeclaration> Units => _units;

    /// <summary>Unit declarations by module id (<c>module &lt;kind&gt; &lt;id&gt;</c>).</summary>
    public IReadOnlyDictionary<string, DashSpecUnitDeclaration> UnitsById => _unitsById;

    /// <summary>Unit declarations grouped by the declared <c>namespace &lt;Ns&gt;</c>.</summary>
    public IReadOnlyDictionary<string, List<DashSpecUnitDeclaration>> UnitsByNamespace => _unitsByNamespace;

    /// <summary>Declared <c>type &lt;RowType&gt;</c> definitions mapped to their source file, 1-based line, and column.</summary>
    public IReadOnlyDictionary<string, (string Path, int Line, int Column)> RowTypes => _rowTypes;

    /// <summary>Cached raw text and parsed syntax tree per full path for zero-disk workspace queries.</summary>
    public IReadOnlyDictionary<string, (string Text, ParseTree Tree)> UnitSources => _unitSources;

    public void RegisterDiagram(string id, string path)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            _diagrams[id] = Path.GetFullPath(path);
        }
    }

    public void RegisterPresentation(string id, string path)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            _presentations[id] = Path.GetFullPath(path);
        }
    }

    public void RegisterUnit(DashSpecUnitDeclaration unit)
    {
        if (string.IsNullOrWhiteSpace(unit.Id) || string.IsNullOrWhiteSpace(unit.Path))
        {
            return;
        }

        unit = unit with { Path = Path.GetFullPath(unit.Path) };
        _units.Add(unit);
        _unitsById[unit.Id] = unit;
        if (!string.IsNullOrWhiteSpace(unit.Namespace))
        {
            if (!_unitsByNamespace.TryGetValue(unit.Namespace, out var list))
            {
                list = [];
                _unitsByNamespace[unit.Namespace] = list;
            }

            if (!list.Any(u => string.Equals(u.Path, unit.Path, StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(unit);
            }
        }

        if (unit.Kind.Equals("diagram", StringComparison.OrdinalIgnoreCase))
        {
            RegisterDiagram(unit.Id, unit.Path);
        }
        else if (unit.Kind.Equals("presentation", StringComparison.OrdinalIgnoreCase))
        {
            RegisterPresentation(unit.Id, unit.Path);
        }
    }

    /// <summary>
    /// Builds the workspace index via <see cref="UnitIndexModule.corpusFiles"/> (<c>.dsproj</c> aware)
    /// and unwraps each unit's typed <see cref="UnitGrammar.UnitEnvelope"/> from its AST.
    /// </summary>
    public static DashSpecWorkspaceIndex Scan(string rootDirectory)
    {
        var index = new DashSpecWorkspaceIndex();
        if (!Directory.Exists(rootDirectory))
        {
            return index;
        }

        foreach (var path in UnitIndexModule.corpusFiles(rootDirectory))
        {
            index.RegisterUnitFile(path);
        }

        return index;
    }

    public static DashSpecWorkspaceIndex BuildFromCorpusTexts(IReadOnlyDictionary<string, string> memberTexts)
    {
        var index = new DashSpecWorkspaceIndex();
        foreach (var (path, text) in memberTexts)
        {
            index.RegisterUnitText(path, text);
        }

        return index;
    }

    public static string? TryReadDiagramId(string text) => TryReadHeaderId(text, "diagram");

    public static string? TryReadPresentationId(string text) => TryReadHeaderId(text, "presentation");

    private static string? TryReadHeaderId(string text, string kindKeyword)
    {
        var (kind, id) = UnitSource.tryKindAndId(text);
        return !string.IsNullOrWhiteSpace(id) && kind.Equals(kindKeyword, StringComparison.OrdinalIgnoreCase)
            ? id
            : null;
    }

    private void RegisterUnitFile(string path)
    {
        var rawOpt = UnitSource.tryReadRaw(path);
        if (!FSharpOption<string>.get_IsSome(rawOpt))
        {
            return;
        }

        RegisterUnitText(path, rawOpt.Value);
    }

    private void RegisterUnitText(string path, string text)
    {
        var fullPath = Path.GetFullPath(path);
        var tree = SyntaxTree.parse(text);
        _unitSources[fullPath] = (text, tree);
        var envOpt = UnitGrammar.tryUnwrapAny(tree.Root);
        if (!FSharpOption<UnitGrammar.UnitEnvelope>.get_IsSome(envOpt))
        {
            return;
        }

        var env = envOpt.Value;
        var kind = DashSpecModuleKindModule.toKeyword(env.Kind);
        var id = env.Id;
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var ns = FSharpOption<string>.get_IsSome(env.Namespace) ? env.Namespace.Value : "";
        var (headerLine, _) = TextPositions.GetLineColumn(text, env.ModuleNode.HeaderSpan.Start);

        RegisterUnit(new DashSpecUnitDeclaration(kind, id, fullPath, headerLine + 1) { Namespace = ns });

        if (kind.Equals("types", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var member in env.Body)
            {
                if (member is DashSpecAstNode.BlockDeclaration b &&
                    b.Item.Opener is DashSpecBlockOpener.Named named &&
                    named.keyword.IsType &&
                    !string.IsNullOrWhiteSpace(named.identifier))
                {
                    var (typeLine, typeCol) = TextPositions.GetLineColumn(text, b.Item.HeaderSpan.Start);
                    _rowTypes.TryAdd(named.identifier, (fullPath, typeLine + 1, typeCol + 1));
                }
            }
        }
    }
}
