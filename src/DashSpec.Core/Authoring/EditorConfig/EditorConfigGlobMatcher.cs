namespace DashSpec.Core.Authoring.EditorConfig;

internal static class EditorConfigGlobMatcher
{
    public static bool SectionMatches(string sectionHeader, string absoluteFilePath, string configDirectory)
    {
        var header = sectionHeader.Trim();
        if (header.Length < 2 || header[0] is not '[' || header[^1] is not ']')
        {
            return false;
        }

        var pattern = header[1..^1].Trim();
        if (pattern.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = ToRelativePath(absoluteFilePath, configDirectory);
        foreach (var expanded in ExpandBraceAlternatives(pattern))
        {
            if (MatchPattern(expanded, Path.GetFileName(absoluteFilePath)) ||
                MatchPattern(expanded, relative))
            {
                return true;
            }
        }

        return false;
    }

    static string ToRelativePath(string absoluteFilePath, string configDirectory)
    {
        var fileDir = Path.GetDirectoryName(absoluteFilePath) ?? string.Empty;
        configDirectory = Path.GetFullPath(configDirectory);
        fileDir = Path.GetFullPath(fileDir);

        if (!fileDir.StartsWith(configDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(absoluteFilePath);
        }

        var relativeDir = Path.GetRelativePath(configDirectory, fileDir);
        var fileName = Path.GetFileName(absoluteFilePath);
        return relativeDir is "." or ""
            ? fileName
            : relativeDir.Replace('\\', '/') + "/" + fileName;
    }

    static IEnumerable<string> ExpandBraceAlternatives(string pattern)
    {
        var start = pattern.IndexOf('{');
        if (start < 0)
        {
            yield return pattern;
            yield break;
        }

        var end = pattern.IndexOf('}', start + 1);
        if (end < 0)
        {
            yield return pattern;
            yield break;
        }

        var prefix = pattern[..start];
        var suffix = pattern[(end + 1)..];
        foreach (var option in pattern[(start + 1)..end].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return prefix + option + suffix;
        }
    }

    static bool MatchPattern(string pattern, string candidate)
    {
        pattern = pattern.Replace('\\', '/');
        candidate = candidate.Replace('\\', '/');

        if (pattern is "*" or "**")
        {
            return true;
        }

        if (pattern.Contains('/'))
        {
            return MatchSimpleGlob(pattern.AsSpan(), candidate.AsSpan());
        }

        if (candidate.Contains('/'))
        {
            var fileName = candidate[(candidate.LastIndexOf('/') + 1)..];
            return MatchSimpleGlob(pattern.AsSpan(), fileName.AsSpan());
        }

        return MatchSimpleGlob(pattern.AsSpan(), candidate.AsSpan());
    }

    static bool MatchSimpleGlob(ReadOnlySpan<char> pattern, ReadOnlySpan<char> candidate)
    {
        var p = 0;
        var c = 0;
        var starPattern = -1;
        var starCandidate = -1;

        while (c < candidate.Length)
        {
            if (p < pattern.Length && (pattern[p] == candidate[c] || pattern[p] is '?'))
            {
                p++;
                c++;
                continue;
            }

            if (p < pattern.Length && pattern[p] is '*')
            {
                starPattern = p++;
                starCandidate = c;
                continue;
            }

            if (starPattern >= 0)
            {
                p = starPattern + 1;
                c = ++starCandidate;
                continue;
            }

            return false;
        }

        while (p < pattern.Length && pattern[p] is '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
