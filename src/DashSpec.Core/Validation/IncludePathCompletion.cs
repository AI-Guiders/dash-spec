namespace DashSpec.Core.Validation;

/// <summary>Suggest relative paths for <c>!include "…"</c> (editor completion).</summary>
public static class IncludePathCompletion
{
    private static readonly string[] Extensions =
    [
        ".dashdiagram",
        ".dashpresentation",
        ".dashspec",
        ".dashinclude",
        ".dashlayout",
        ".dashpalette",
        ".dashtransform",
    ];

    public static IReadOnlyList<string> Suggest(string specDirectory, string partial)
    {
        if (string.IsNullOrWhiteSpace(specDirectory) || !Directory.Exists(specDirectory))
        {
            return [];
        }

        partial = partial.Replace('\\', '/');
        var suggestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(partial))
        {
            AddRootFiles(specDirectory, specDirectory, string.Empty, suggestions);
            AddMatchingDirectories(specDirectory, specDirectory, string.Empty, suggestions);
            AddGlobPresets(specDirectory, suggestions);
            return Sort(suggestions);
        }

        if (partial.EndsWith('/'))
        {
            var targetDir = ResolveDirectory(specDirectory, partial);
            if (Directory.Exists(targetDir))
            {
                AddRootFiles(specDirectory, targetDir, string.Empty, suggestions);
                AddMatchingDirectories(specDirectory, targetDir, string.Empty, suggestions);
            }

            return Sort(suggestions);
        }

        var lastSlash = partial.LastIndexOf('/');
        if (lastSlash < 0)
        {
            AddRootFiles(specDirectory, specDirectory, partial, suggestions);
            AddMatchingDirectories(specDirectory, specDirectory, partial, suggestions);
            return Sort(suggestions);
        }

        var dirPart = partial[..(lastSlash + 1)];
        var filePrefix = partial[(lastSlash + 1)..];
        var searchDir = ResolveDirectory(specDirectory, dirPart);
        if (Directory.Exists(searchDir))
        {
            AddRootFiles(specDirectory, searchDir, filePrefix, suggestions);
            if (string.IsNullOrEmpty(filePrefix))
            {
                AddMatchingDirectories(specDirectory, searchDir, string.Empty, suggestions);
            }
        }

        return Sort(suggestions);
    }

    private static void AddRootFiles(
        string specDirectory,
        string searchDirectory,
        string filePrefix,
        HashSet<string> suggestions)
    {
        foreach (var extension in Extensions)
        {
            foreach (var file in Directory.EnumerateFiles(searchDirectory, $"*{extension}", SearchOption.TopDirectoryOnly))
            {
                var relative = ToRelative(specDirectory, file);
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(filePrefix) ||
                    fileName.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add(relative);
                }
            }
        }
    }

    private static void AddMatchingDirectories(
        string specDirectory,
        string searchDirectory,
        string directoryPrefix,
        HashSet<string> suggestions)
    {
        foreach (var directory in Directory.EnumerateDirectories(searchDirectory))
        {
            var name = Path.GetFileName(directory);
            if (string.IsNullOrEmpty(directoryPrefix) ||
                name.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var relativeDir = ToRelative(specDirectory, directory).TrimEnd('/') + "/";
                suggestions.Add(relativeDir);
            }
        }
    }

    private static void AddGlobPresets(string specDirectory, HashSet<string> suggestions)
    {
        foreach (var extension in Extensions)
        {
            foreach (var directory in Directory.EnumerateDirectories(specDirectory))
            {
                var folder = Path.GetFileName(directory);
                if (Directory.EnumerateFiles(directory, $"*{extension}").Any())
                {
                    suggestions.Add($"{folder}/*{extension}");
                }
            }
        }
    }

    private static string ResolveDirectory(string specDirectory, string relativeDir)
    {
        var combined = relativeDir.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(specDirectory, combined));
    }

    private static string ToRelative(string specDirectory, string fullPath)
    {
        return Path.GetRelativePath(specDirectory, fullPath).Replace('\\', '/');
    }

    private static List<string> Sort(IEnumerable<string> suggestions) =>
        suggestions.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
}
