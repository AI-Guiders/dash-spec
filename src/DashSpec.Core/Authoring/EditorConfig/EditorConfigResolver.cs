namespace DashSpec.Core.Authoring.EditorConfig;

/// <summary>Walks up from a file directory, merges matching .editorconfig sections (nearest wins).</summary>
public static class EditorConfigResolver
{
    public static EditorConfigOptions ResolveForFile(string absoluteFilePath, string? searchRoot = null)
    {
        absoluteFilePath = Path.GetFullPath(absoluteFilePath);
        var directory = Path.GetDirectoryName(absoluteFilePath) ?? absoluteFilePath;
        searchRoot = searchRoot is null ? null : Path.GetFullPath(searchRoot);

        var configPaths = new List<string>();
        var dir = directory;
        while (!string.IsNullOrEmpty(dir))
        {
            var configPath = Path.Combine(dir, ".editorconfig");
            if (File.Exists(configPath))
            {
                configPaths.Add(configPath);
            }

            if (searchRoot is not null &&
                dir.Equals(searchRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent.Equals(dir, StringComparison.Ordinal))
            {
                break;
            }

            dir = parent;
        }

        if (configPaths.Count == 0)
        {
            return EditorConfigOptions.Default;
        }

        EditorConfigOptions merged = EditorConfigOptions.Default;
        for (var i = configPaths.Count - 1; i >= 0; i--)
        {
            var configDir = Path.GetDirectoryName(configPaths[i]) ?? string.Empty;
            merged = ApplyPatch(merged, ParseConfigFile(configPaths[i], absoluteFilePath, configDir));
        }

        return merged;
    }

    static EditorConfigPatch ParseConfigFile(string configPath, string absoluteFilePath, string configDirectory)
    {
        var patch = new EditorConfigPatch();
        var inSection = false;

        foreach (var rawLine in File.ReadAllLines(configPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                inSection = EditorConfigGlobMatcher.SectionMatches(line, absoluteFilePath, configDirectory);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Contains(':'))
            {
                value = value[..value.IndexOf(':')].Trim();
            }

            if (value.Equals("unset", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            patch.Set(key, value);
        }

        return patch;
    }

    static EditorConfigOptions ApplyPatch(EditorConfigOptions baseline, EditorConfigPatch patch) => baseline with
    {
        IndentStyle = patch.HasIndentStyle ? patch.IndentStyle : baseline.IndentStyle,
        IndentSize = patch.HasIndentSize ? patch.IndentSize : baseline.IndentSize,
        TabWidth = patch.HasTabWidth ? patch.TabWidth : baseline.TabWidth,
        EndOfLine = patch.HasEndOfLine ? patch.EndOfLine : baseline.EndOfLine,
        TrimTrailingWhitespace = patch.HasTrimTrailingWhitespace ? patch.TrimTrailingWhitespace : baseline.TrimTrailingWhitespace,
        InsertFinalNewline = patch.HasInsertFinalNewline ? patch.InsertFinalNewline : baseline.InsertFinalNewline,
        DashSpecFormatOnSave = patch.HasDashSpecFormatOnSave ? patch.DashSpecFormatOnSave : baseline.DashSpecFormatOnSave,
        DashSpecMaxConsecutiveBlankLines = patch.HasDashSpecMaxConsecutiveBlankLines
            ? patch.DashSpecMaxConsecutiveBlankLines
            : baseline.DashSpecMaxConsecutiveBlankLines,
        DashSpecIndentBlockBody = patch.HasDashSpecIndentBlockBody ? patch.DashSpecIndentBlockBody : baseline.DashSpecIndentBlockBody,
        DashSpecPreserveBlankLineBeforeEnd = patch.HasDashSpecPreserveBlankLineBeforeEnd
            ? patch.DashSpecPreserveBlankLineBeforeEnd
            : baseline.DashSpecPreserveBlankLineBeforeEnd,
    };
}
