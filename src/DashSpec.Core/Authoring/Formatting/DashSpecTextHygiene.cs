namespace DashSpec.Core.Authoring.Formatting;

public static class DashSpecTextHygiene
{
    public static string Apply(string text, EditorConfig.EditorConfigOptions options)
    {
        var normalized = NormalizeNewlines(text);
        var lines = normalized.Split('\n');
        var output = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            var current = options.TrimTrailingWhitespace ? line.TrimEnd() : line;
            output.Add(current);
        }

        var collapsed = CollapseBlankLines(output, options.DashSpecMaxConsecutiveBlankLines);
        var joined = string.Join(options.NewLine, collapsed);
        if (options.InsertFinalNewline && (joined.Length == 0 || !joined.EndsWith(options.NewLine, StringComparison.Ordinal)))
        {
            joined += options.NewLine;
        }

        return joined;
    }

    public static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    static List<string> CollapseBlankLines(IReadOnlyList<string> lines, int maxConsecutiveBlankLines)
    {
        if (maxConsecutiveBlankLines < 0)
        {
            maxConsecutiveBlankLines = 0;
        }

        var output = new List<string>(lines.Count);
        var blankRun = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                blankRun++;
                if (blankRun <= maxConsecutiveBlankLines)
                {
                    output.Add(string.Empty);
                }

                continue;
            }

            blankRun = 0;
            output.Add(line);
        }

        while (output.Count > 0 && output[^1].Length == 0)
        {
            output.RemoveAt(output.Count - 1);
        }

        return output;
    }
}
