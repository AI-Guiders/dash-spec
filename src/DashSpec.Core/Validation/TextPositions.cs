using System.Text.RegularExpressions;

namespace DashSpec.Core.Validation;

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
        var tokenEnd = Math.Min(text.Length, offset + 1);
        var (endLine, endChar) = GetLineColumn(text, tokenEnd);
        return new DashSpecDiagnostic(line, character, endLine, endChar + 1, message);
    }

    [GeneratedRegex(@"at position\s+(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PositionMessagePattern();
}
