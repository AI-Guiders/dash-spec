using System.Text.RegularExpressions;
using FsharpDocument = DashSpec.Modeling.Parse.Document;

namespace DashSpec.Execution.Compilation;

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
            CompletionKind.Flow => BuildIdItems(
                workspaceIndex?.Units.Where(u => u.Kind.Equals("flow", StringComparison.OrdinalIgnoreCase))
                    .Select(u => u.Id) ?? [],
                context.Value.Prefix,
                "flow"),
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
        var target = kind switch
        {
            DefinitionKind.Diagram when workspaceIndex?.UnitsById.TryGetValue(word, out var diagram) == true
                => (diagram.Path, diagram.Line),
            DefinitionKind.Presentation when workspaceIndex?.UnitsById.TryGetValue(word, out var presentation) == true
                => (presentation.Path, presentation.Line),
            DefinitionKind.Flow when workspaceIndex?.UnitsById.TryGetValue(word, out var flow) == true
                => (flow.Path, flow.Line),
            DefinitionKind.Namespace when workspaceIndex?.UnitsByNamespace.TryGetValue(word, out var units) == true
                && units.Count > 0
                => (units[0].Path, units[0].Line),
            _ => ((string?)null, 0),
        };

        return string.IsNullOrWhiteSpace(target.Item1) || !File.Exists(target.Item1)
            ? null
            : new DefinitionTarget(target.Item1, target.Item2, 1);
    }

    /// <summary>
    /// Cross-file find-usages for a workspace unit id (diagram / flow / presentation / …):
    /// isolated-word occurrences across the corpus `.ds` files, target = the module declaration.
    /// </summary>
    public static IReadOnlyList<WorkspaceUsage> FindWorkspaceUsages(
        string symbolName,
        DashSpecWorkspaceIndex? workspaceIndex)
    {
        if (string.IsNullOrWhiteSpace(symbolName) || workspaceIndex is null)
        {
            return [];
        }

        if (!workspaceIndex.UnitsById.TryGetValue(symbolName, out var unit))
        {
            return [];
        }

        var wordRegex = new Regex(
            $@"(?<![A-Za-z0-9_]){Regex.Escape(symbolName)}(?![A-Za-z0-9_])",
            RegexOptions.None);
        var usages = new List<WorkspaceUsage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in workspaceIndex.Units.Select(u => u.Path)
                     .Concat(workspaceIndex.UnitsByNamespace.Values.SelectMany(v => v.Select(u => u.Path)))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                continue;
            }

            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match match in wordRegex.Matches(lines[i].TrimEnd('\r')))
                {
                    var key = $"{path}:{i + 1}:{match.Index + 1}";
                    if (seen.Add(key))
                    {
                        usages.Add(new WorkspaceUsage(
                            path,
                            i + 1,
                            match.Index + 1,
                            i + 1,
                            match.Index + 1 + match.Length));
                    }
                }
            }
        }

        return usages;
    }

    public sealed record WorkspaceUsage(string Path, int Line, int Column, int EndLine, int EndColumn);

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

        // Living diagram form: `diagram <id>` and `diagram ref <slot> <id>`.
        var diagramMatch = Regex.Match(
            before,
            @"\bdiagram\s+(?:ref\s+\w+\s+)?([\w.-]*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (diagramMatch.Success)
        {
            return (diagramMatch.Groups[1].Value, CompletionKind.Diagram);
        }

        // `use flow <id>` → flow module ids.
        var flowMatch = Regex.Match(
            before,
            @"\buse\s+flow\s+([\w.-]*)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (flowMatch.Success)
        {
            return (flowMatch.Groups[1].Value, CompletionKind.Flow);
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

        // `diagram <id>` / `diagram ref <slot> <id>` — the id is the diagram module id
        // (anywhere later on the line, after the optional slot ref).
        if (Regex.IsMatch(
                line,
                $@"\bdiagram\b.*\b{Regex.Escape(word)}\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return DefinitionKind.Diagram;
        }

        if (RegexIsolatedWord(line, "use", word) &&
            (line.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("presentation", StringComparison.OrdinalIgnoreCase)))
        {
            return DefinitionKind.Presentation;
        }

        // `use flow <id>` → the flow module (`module flow <id>`).
        if (RegexIsolatedWord(line, "flow", word) ||
            RegexIsolatedWord(line, "flows", word))
        {
            return DefinitionKind.Flow;
        }

        // `import <kind> from <Namespace>` → the unit file declaring `namespace <Namespace>`.
        if (Regex.IsMatch(
                line,
                $@"\bimport\s+\w+\s+from\s+{Regex.Escape(word)}\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return DefinitionKind.Namespace;
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
        Flow,
        Palette,
    }

    enum DefinitionKind
    {
        Diagram,
        Presentation,
        Flow,
        Namespace,
    }
}
