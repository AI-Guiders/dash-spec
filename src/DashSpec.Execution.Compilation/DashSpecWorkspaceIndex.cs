using System.Text.RegularExpressions;
using FsharpDocument = DashSpec.Modeling.Parse.Document;

namespace DashSpec.Execution.Compilation;

public sealed record DashSpecUnitDeclaration(string Kind, string Id, string Path, int Line)
{
    public string Namespace { get; init; } = "";
}

/// <summary>
/// Workspace unit index for LSP completion, go-to-definition and find-usages:
/// every `module &lt;kind&gt; &lt;id&gt;` header (tab / dashboard / diagram / flow /
/// presentation / layout / palette / types / …) plus `namespace &lt;Ns&gt;` → file + line.
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

    public IReadOnlyDictionary<string, string> Diagrams => _diagrams;

    public IReadOnlyDictionary<string, string> Presentations => _presentations;

    public IReadOnlyList<DashSpecUnitDeclaration> Units => _units;

    /// <summary>Unit declarations by module id (`module &lt;kind&gt; &lt;id&gt;`).</summary>
    public IReadOnlyDictionary<string, DashSpecUnitDeclaration> UnitsById => _unitsById;

    /// <summary>Unit declarations grouped by the declared `namespace &lt;Ns&gt;`.</summary>
    public IReadOnlyDictionary<string, List<DashSpecUnitDeclaration>> UnitsByNamespace => _unitsByNamespace;

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

    /// <summary>Scans the `.ds` units of a workspace (ADR-0101) and registers every module kind.</summary>
    public static DashSpecWorkspaceIndex Scan(string rootDirectory)
    {
        var index = new DashSpecWorkspaceIndex();
        if (!Directory.Exists(rootDirectory))
        {
            return index;
        }

        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".ds", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".dashspec", StringComparison.OrdinalIgnoreCase))
            {
                index.RegisterUnitFile(path);
            }
        }

        return index;
    }

    public static string? TryReadDiagramId(string text) => TryReadHeaderId(text, "diagram");

    public static string? TryReadPresentationId(string text) => TryReadHeaderId(text, "presentation");

    /// <summary>Reads `module &lt;kind&gt; &lt;id&gt;` (ADR-0101) through the language's own header reader.</summary>
    private static string? TryReadHeaderId(string text, string kindKeyword)
    {
        var (kind, id) = FsharpDocument.UnitSource.tryKindAndId(text);

        return !string.IsNullOrWhiteSpace(id) && kind.Equals(kindKeyword, StringComparison.OrdinalIgnoreCase)
            ? id
            : null;
    }

    private static readonly Regex ModuleHeaderRegex = new(
        @"^\s*module\s+(?<kind>[A-Za-z_][A-Za-z0-9_]*)\s+(?<id>[A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex = new(
        @"^\s*namespace\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)",
        RegexOptions.Compiled);

    private void RegisterUnitFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            return;
        }

        var lines = text.Split('\n');
        var headerLine = Array.FindIndex(lines, l => ModuleHeaderRegex.IsMatch(l.TrimEnd('\r')));
        if (headerLine < 0)
        {
            return;
        }

        var match = ModuleHeaderRegex.Match(lines[headerLine].TrimEnd('\r'));
        var kind = match.Groups["kind"].Value;
        var id = match.Groups["id"].Value;
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var nsLine = Array.FindIndex(lines, l => NamespaceRegex.IsMatch(l.TrimEnd('\r')));
        var ns = nsLine >= 0
            ? NamespaceRegex.Match(lines[nsLine].TrimEnd('\r')).Groups["ns"].Value
            : "";

        RegisterUnit(new DashSpecUnitDeclaration(kind, id, path, headerLine + 1) { Namespace = ns });
    }
}
