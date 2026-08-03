using System.Text.RegularExpressions;

namespace DashSpec.Core.Parsing;

internal static partial class IncludeReferenceHeuristics
{
    /// <summary>Path still being typed in the editor (e.g. <c>layouts/</c> before filename).</summary>
    public static bool IsIncomplete(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return true;
        }

        if (reference.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        return reference.EndsWith('/') || reference.EndsWith('\\');
    }

    public static int? TryFindIncludeOffset(string text, string missingPath, string specDirectory)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(missingPath))
        {
            return null;
        }

        var normalizedMissing = NormalizePath(missingPath);
        foreach (Match match in ModuleIncludePattern().Matches(text))
        {
            var reference = match.Groups[1].Value;
            if (!ReferenceResolvesTo(reference, specDirectory, normalizedMissing))
            {
                continue;
            }

            var quoteIndex = text.IndexOf('"', match.Index, match.Length);
            return quoteIndex >= 0 ? quoteIndex : match.Index;
        }

        return null;
    }

    private static bool ReferenceResolvesTo(string reference, string specDirectory, string normalizedMissing)
    {
        var resolved = SpecIncludeResolver.ResolvePath(reference, specDirectory);
        foreach (var candidate in EnumerateIncludePathCandidates(resolved))
        {
            if (string.Equals(NormalizePath(candidate), normalizedMissing, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateIncludePathCandidates(string path)
    {
        yield return path;
        foreach (var ext in new[] { ".dashlayout", ".dashdiagram", ".dashinclude", ".dashpresentation" })
        {
            if (!path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                yield return path + ext;
            }
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.TrimEnd('"', '\''));

    [GeneratedRegex(@"!include\s+""([^""]*)""", RegexOptions.Multiline)]
    private static partial Regex ModuleIncludePattern();
}
