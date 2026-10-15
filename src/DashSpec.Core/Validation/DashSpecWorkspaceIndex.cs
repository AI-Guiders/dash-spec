using DashSpec.Core.Parsing;

namespace DashSpec.Core.Validation;

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
            if (ext.Equals(".dashdiagram", StringComparison.OrdinalIgnoreCase))
            {
                index.RegisterDiagramFile(path);
            }
            else if (ext.Equals(".dashpresentation", StringComparison.OrdinalIgnoreCase))
            {
                index.RegisterPresentationFile(path);
            }
        }

        return index;
    }

    public static string? TryReadDiagramId(string text) => TryReadModuleId(text, "diagram");

    public static string? TryReadPresentationId(string text) => TryReadModuleId(text, "presentation");

    private void RegisterDiagramFile(string path)
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

        var id = TryReadDiagramId(text);
        if (!string.IsNullOrWhiteSpace(id))
        {
            RegisterDiagram(id, path);
        }
    }

    private void RegisterPresentationFile(string path)
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

        var id = TryReadPresentationId(text);
        if (!string.IsNullOrWhiteSpace(id))
        {
            RegisterPresentation(id, path);
        }
    }

    private static string? TryReadModuleId(string text, string keyword)
    {
        try
        {
            var reader = ParserUtilities.CreateReader(text);
            reader.SkipFileDirectives();
            if (!reader.IsAt(TokenKind.At))
            {
                return null;
            }

            reader.Advance();
            if (!reader.TryKeyword(keyword))
            {
                return null;
            }

            return reader.ReadIdent();
        }
        catch
        {
            return null;
        }
    }
}
