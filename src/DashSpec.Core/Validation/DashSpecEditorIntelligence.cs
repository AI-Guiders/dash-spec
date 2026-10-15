using System.Text.RegularExpressions;
using FsharpDocument = DashSpec.Modeling.Parse.Document;

namespace DashSpec.Core.Validation;

/// <summary>In-process DashSpec editor intelligence (LSP parity without OmniSharp transport).</summary>
public static class DashSpecEditorIntelligence
{
    static readonly string[] Keywords =
    [
        "module dashboard", "module tab", "module diagram", "module presentation", "module palette",
        "card", "diagram", "filter", "chrome", "use",
        "end", "heatmap", "gantt", "bar", "line", "table", "toolbar", "filters",
        "datasource", "phase", "page", "catalog", "report", "bind", "layout",
        "presentation", "palette",
    ];

    public sealed record CompletionSuggestion(string Label, string InsertText, string? Kind = null, string? Detail = null);

    public sealed record DefinitionTarget(string FilePath, int Line, int Column);

    public static IReadOnlyList<CompletionSuggestion> GetCompletions(
        string filePath,
        string text,
        int line,
        int column,
        DashSpecWorkspaceIndex? workspaceIndex = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = text.Split('\n');
        var lineIndex = line - 1;
        if (lineIndex < 0 || lineIndex >= lines.Length)
        {
            return [];
        }

        var currentLine = lines[lineIndex].TrimEnd('\r');
        var context = GetCompletionContext(currentLine, column);
        return context?.Kind switch
        {
            CompletionKind.Diagram => BuildIdItems(
                workspaceIndex?.Diagrams.Keys ?? [],
                context.Value.Prefix,
                "diagram"),
            CompletionKind.Presentation => BuildIdItems(
                workspaceIndex?.Presentations.Keys ?? [],
                context.Value.Prefix,
                "presentation"),
            CompletionKind.Palette => BuildIdItems(
                ListPaletteIds(GetSpecDirectory(filePath)),
                context.Value.Prefix,
                "palette"),
            _ => BuildKeywordItems(currentLine, column),
        };
    }

    public static IReadOnlyList<CompletionSuggestion> GetCompletionsAtOffset(
        string filePath,
        string text,
        int caretOffset,
        DashSpecWorkspaceIndex? workspaceIndex = null)
    {
        var (line, character) = TextPositions.GetLineColumn(text, caretOffset);
        return GetCompletions(filePath, text, line + 1, character + 1, workspaceIndex);
    }

    public static DefinitionTarget? TryGetDefinition(
        string filePath,
        string text,
        int line,
        int column,
        DashSpecWorkspaceIndex? workspaceIndex = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var lines = text.Split('\n');
        var lineIndex = line - 1;
        if (lineIndex < 0 || lineIndex >= lines.Length)
        {
            return null;
        }

        var currentLine = lines[lineIndex].TrimEnd('\r');
        var word = GetWordAt(currentLine, column);
        if (string.IsNullOrWhiteSpace(word))
        {
            return null;
        }

        var kind = GetDefinitionKind(currentLine, word);
        var targetPath = kind switch
        {
            DefinitionKind.Diagram when workspaceIndex?.Diagrams.TryGetValue(word, out var diagramPath) == true
                => diagramPath,
            DefinitionKind.Presentation when workspaceIndex?.Presentations.TryGetValue(word, out var presentationPath) == true
                => presentationPath,
            _ => null,
        };

        return string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath)
            ? null
            : new DefinitionTarget(targetPath, 1, 1);
    }

    public static DashSpecWorkspaceIndex ResolveWorkspaceIndex(string? projectRoot, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot) && Directory.Exists(projectRoot))
        {
            return DashSpecWorkspaceIndex.Scan(projectRoot);
        }

        var directory = GetSpecDirectory(filePath);
        return Directory.Exists(directory) ? DashSpecWorkspaceIndex.Scan(directory) : new DashSpecWorkspaceIndex();
    }

    static IEnumerable<string> ListPaletteIds(string directory)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.ds", SearchOption.AllDirectories))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var (kind, id) = FsharpDocument.UnitSource.tryKindAndId(text);
            if (!string.IsNullOrWhiteSpace(id) && kind.Equals("palette", StringComparison.OrdinalIgnoreCase))
            {
                yield return id;
            }
        }
    }

    static IReadOnlyList<CompletionSuggestion> BuildIdItems(
        IEnumerable<string> ids,
        string prefix,
        string kind) =>
        ids.Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Select(id => new CompletionSuggestion(id, id, Kind: kind))
            .ToArray();

    static IReadOnlyList<CompletionSuggestion> BuildKeywordItems(string line, int character)
    {
        var before = line[..Math.Clamp(character, 0, line.Length)];
        var wordStart = before.Length;
        while (wordStart > 0 && (char.IsLetterOrDigit(before[wordStart - 1]) || before[wordStart - 1] is '_' or '@' or '!'))
        {
            wordStart--;
        }

        var prefix = before[wordStart..];
        return Keywords
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => new CompletionSuggestion(
                k,
                k,
                Kind: k.StartsWith('@') ? "class" : "keyword"))
            .ToArray();
    }

    static (string Prefix, CompletionKind Kind)? GetCompletionContext(string line, int character)
    {
        var before = line[..Math.Clamp(character, 0, line.Length)];

        if (TryMatchKeywordPrefix(before, "diagram", out var diagramPrefix))
        {
            return (diagramPrefix, CompletionKind.Diagram);
        }

        if (TryMatchKeywordPrefix(before, "use", out var usePrefix) &&
            (before.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
             before.Contains("presentation", StringComparison.OrdinalIgnoreCase)))
        {
            return (usePrefix, CompletionKind.Presentation);
        }

        if (TryMatchKeywordPrefix(before, "preset", out var presetPrefix) &&
            before.Contains("use", StringComparison.OrdinalIgnoreCase))
        {
            return (presetPrefix, CompletionKind.Presentation);
        }

        if (TryMatchKeywordPrefix(before, "palette", out var palettePrefix))
        {
            return (palettePrefix, CompletionKind.Palette);
        }

        return null;
    }

    static DefinitionKind? GetDefinitionKind(string line, string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return null;
        }

        if (RegexIsolatedWord(line, "diagram", word))
        {
            return DefinitionKind.Diagram;
        }

        if (RegexIsolatedWord(line, "use", word) &&
            (line.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("presentation", StringComparison.OrdinalIgnoreCase)))
        {
            return DefinitionKind.Presentation;
        }

        return null;
    }

    static string GetSpecDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        return string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
    }

    static bool TryMatchKeywordPrefix(string before, string keyword, out string prefix)
    {
        prefix = string.Empty;
        var match = Regex.Match(
            before,
            $@"\b{keyword}\s+([\w.-]*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        prefix = match.Groups[1].Value;
        return true;
    }

    static bool RegexIsolatedWord(string line, string keyword, string word) =>
        Regex.IsMatch(
            line,
            $@"\b{keyword}\s+{Regex.Escape(word)}\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static string GetWordAt(string line, int character)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        var index = Math.Clamp(character, 0, line.Length);
        var start = index;
        while (start > 0 && IsIdentChar(line[start - 1]))
        {
            start--;
        }

        var end = index;
        while (end < line.Length && IsIdentChar(line[end]))
        {
            end++;
        }

        return line[start..end];
    }

    static bool IsIdentChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.';

    enum CompletionKind
    {
        Diagram,
        Presentation,
        Palette,
    }

    enum DefinitionKind
    {
        Diagram,
        Presentation,
    }
}
