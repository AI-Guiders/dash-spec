using System.Text.RegularExpressions;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Validation;

/// <summary>Diagram / presentation ids → file paths for LSP completion and go-to-definition.</summary>
public sealed class DashSpecWorkspaceIndex
{
    private static readonly Regex IncludePattern = new(
        @"(?:!include|import)\s+""([^""]+)""",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

    public void RegisterIncludesFromText(string text, string baseDirectory)
    {
        foreach (Match match in IncludePattern.Matches(text))
        {
            var reference = match.Groups[1].Value;
            foreach (var resolved in ResolveIncludePaths(reference, baseDirectory))
            {
                var ext = Path.GetExtension(resolved);
                if (ext.Equals(".dashdiagram", StringComparison.OrdinalIgnoreCase))
                {
                    RegisterDiagramFile(resolved);
                }
                else if (ext.Equals(".dashpresentation", StringComparison.OrdinalIgnoreCase))
                {
                    RegisterPresentationFile(resolved);
                }
            }
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
            else if (ext.Equals(".dashspec", StringComparison.OrdinalIgnoreCase) ||
                     ext.Equals(".dashinclude", StringComparison.OrdinalIgnoreCase))
            {
                index.RegisterIncludesFromSpec(path, rootDirectory);
            }
        }

        return index;
    }

    public static string? TryReadDiagramId(string text) => TryReadModuleId(text, "diagram");

    public static string? TryReadPresentationId(string text) => TryReadModuleId(text, "presentation");

    private void RegisterIncludesFromSpec(string specPath, string rootDirectory)
    {
        string text;
        try
        {
            text = File.ReadAllText(specPath);
        }
        catch
        {
            return;
        }

        var baseDir = Path.GetDirectoryName(specPath) ?? rootDirectory;
        RegisterIncludesFromText(text, baseDir);
    }

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

    private static IEnumerable<string> ResolveIncludePaths(string reference, string baseDirectory)
    {
        if (!reference.Contains('*', StringComparison.Ordinal))
        {
            var path = SpecIncludeResolver.ResolvePath(reference, baseDirectory);
            if (File.Exists(path))
            {
                yield return path;
            }
            else if (File.Exists(path + ".dashdiagram"))
            {
                yield return path + ".dashdiagram";
            }
            else if (File.Exists(path + ".dashpresentation"))
            {
                yield return path + ".dashpresentation";
            }

            yield break;
        }

        var combined = Path.GetFullPath(Path.Combine(baseDirectory, reference));
        var directory = Path.GetDirectoryName(combined);
        var pattern = Path.GetFileName(combined);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(pattern) ||
            !Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            yield return file;
        }
    }
}
