using System.Text.RegularExpressions;

using DashSpec.Execution.Runtime;

using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Compilation;

public static partial class TextPositions
{
    public static (int Line, int Character) GetLineColumn(string text, int offset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        offset = Math.Clamp(offset, 0, Math.Max(0, text.Length - 1));
        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] is '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart);
    }

    public static int? TryParseOffsetFromMessage(string message)
    {
        var match = PositionMessagePattern().Match(message);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var offset) ? offset : null;
    }

    public static DashSpecDiagnostic ToDiagnostic(string text, string message, int? sourceOffset)
    {
        var offset = sourceOffset ?? TryParseOffsetFromMessage(message) ?? 0;
        offset = Math.Clamp(offset, 0, string.IsNullOrEmpty(text) ? 0 : text.Length - 1);
        var (line, character) = GetLineColumn(text, offset);

        // Expand end span to the full identifier/line segment so squiggles are clearly visible
        var tokenEnd = offset;
        while (tokenEnd < text.Length && text[tokenEnd] is not ('\r' or '\n'))
        {
            tokenEnd++;
        }

        if (tokenEnd <= offset)
        {
            tokenEnd = Math.Min(text.Length, offset + 1);
        }

        var (endLine, endChar) = GetLineColumn(text, tokenEnd);
        return new DashSpecDiagnostic(line, character, endLine, Math.Max(character + 1, endChar), message);
    }

    [GeneratedRegex(@"at position\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PositionMessagePattern();
}
