using DashSpec.Core.Parsing;
using FsharpDocument = DashSpec.Modeling.Parse.Document;

namespace DashSpec.Execution.Compilation;

/// <summary>Diagram / presentation ids → file paths for LSP completion and go-to-definition.</summary>
public sealed class DashSpecWorkspaceIndex
{
    private readonly Dictionary<string, string> _diagrams =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _presentations =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Diagrams => _diagrams;

    public IReadOnlyDictionary<string, string> Presentations => _presentations;

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

    /// <summary>Scans the `.ds` units of a workspace (ADR-0101) and registers the diagram / presentation ones.</summary>
    public static DashSpecWorkspaceIndex Scan(string rootDirectory)
    {
        var index = new DashSpecWorkspaceIndex();
        if (!Directory.Exists(rootDirectory))
        {
            return index;
        }

        foreach (var path in Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(path).Equals(".ds", StringComparison.OrdinalIgnoreCase))
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

        var (kind, id) = FsharpDocument.UnitSource.tryKindAndId(text);
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (kind.Equals("diagram", StringComparison.OrdinalIgnoreCase))
        {
            RegisterDiagram(id, path);
        }
        else if (kind.Equals("presentation", StringComparison.OrdinalIgnoreCase))
        {
            RegisterPresentation(id, path);
        }
    }
}
