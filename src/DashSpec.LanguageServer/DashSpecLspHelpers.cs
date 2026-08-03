using DashSpec.Core.Validation;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace DashSpec.LanguageServer;

internal static class DashSpecLspHelpers
{
    public static readonly TextDocumentSelector DocumentSelector = new(
        new TextDocumentFilter { Language = "dashspec" });

    public static string ToPath(DocumentUri uri) => uri.GetFileSystemPath();

    public static DocumentUri ToUri(string path) => DocumentUri.FromFileSystemPath(Path.GetFullPath(path));

    public static Diagnostic ToLspDiagnostic(DashSpecDiagnostic diagnostic)
    {
        var severity = diagnostic.Severity switch
        {
            DashSpecDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            DashSpecDiagnosticSeverity.Information => DiagnosticSeverity.Information,
            _ => DiagnosticSeverity.Error,
        };

        return new Diagnostic
        {
            Range = new LspRange(
                new Position(diagnostic.Line, diagnostic.Character),
                new Position(diagnostic.EndLine, diagnostic.EndCharacter)),
            Severity = severity,
            Source = "dashspec",
            Message = diagnostic.Message,
        };
    }

    public static string GetSpecDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        return string.IsNullOrWhiteSpace(directory) ? Environment.CurrentDirectory : directory;
    }

    public static (string Prefix, CompletionKind Kind)? GetCompletionContext(string line, int character)
    {
        var before = line[..Math.Clamp(character, 0, line.Length)];

        if (before.Contains("!include", StringComparison.OrdinalIgnoreCase) &&
            before.LastIndexOf('"') > before.LastIndexOf('!'))
        {
            return (ExtractPartialInclude(before), CompletionKind.Include);
        }

        if (TryMatchKeywordPrefix(before, "diagram", out var diagramPrefix))
        {
            return (diagramPrefix, CompletionKind.Diagram);
        }

        if (TryMatchKeywordPrefix(before, "use", out var usePrefix) &&
            (before.Contains("chrome", StringComparison.OrdinalIgnoreCase) ||
             before.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
             before.Contains("include", StringComparison.OrdinalIgnoreCase)))
        {
            return (usePrefix, CompletionKind.Presentation);
        }

        if (TryMatchKeywordPrefix(before, "preset", out var presetPrefix) &&
            before.Contains("use", StringComparison.OrdinalIgnoreCase))
        {
            return (presetPrefix, CompletionKind.Presentation);
        }

        return null;
    }

    public static DefinitionKind? GetDefinitionKind(string line, string word)
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

        if (line.Contains("!include", StringComparison.OrdinalIgnoreCase) &&
            line.Contains(word, StringComparison.Ordinal))
        {
            return DefinitionKind.Include;
        }

        return null;
    }

    private static string ExtractPartialInclude(string before)
    {
        var start = before.LastIndexOf('"');
        return start < 0 ? string.Empty : before[(start + 1)..];
    }

    private static bool TryMatchKeywordPrefix(string before, string keyword, out string prefix)
    {
        prefix = string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(
            before,
            $@"\b{keyword}\s+([\w.-]*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        prefix = match.Groups[1].Value;
        return true;
    }

    private static bool RegexIsolatedWord(string line, string keyword, string word)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            line,
            $@"\b{keyword}\s+{System.Text.RegularExpressions.Regex.Escape(word)}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    internal enum CompletionKind
    {
        Diagram,
        Presentation,
        Include,
    }

    internal enum DefinitionKind
    {
        Diagram,
        Presentation,
        Include,
    }
}
