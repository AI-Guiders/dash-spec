namespace DashSpec.Core.Authoring.Formatting;

/// <summary>Basic-like block reindent for DashSpec end-syntax (ADR-0036, DASHSPEC-ADR-0053).</summary>
public static class DashSpecTextFormatter
{
    readonly record struct BlockFrame(int EndIndent, int ContentIndent);

    public static string Format(string text, EditorConfig.EditorConfigOptions options)
    {
        if (!options.DashSpecIndentBlockBody)
        {
            return DashSpecTextHygiene.NormalizeNewlines(text);
        }

        var normalized = DashSpecTextHygiene.NormalizeNewlines(text);
        var rawLines = normalized.Split('\n');
        var output = new List<string>(rawLines.Length);
        var indentStack = new Stack<BlockFrame>();
        var moduleStarted = false;
        var previousNonBlank = string.Empty;

        foreach (var rawLine in rawLines)
        {
            if (rawLine.Length == 0)
            {
                output.Add(string.Empty);
                continue;
            }

            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                output.Add(string.Empty);
                continue;
            }

            if (IsEndLine(trimmed))
            {
                if (options.DashSpecPreserveBlankLineBeforeEnd &&
                    output.Count > 0 &&
                    output[^1].Length > 0 &&
                    !string.IsNullOrWhiteSpace(previousNonBlank) &&
                    !IsEndLine(previousNonBlank))
                {
                    output.Add(string.Empty);
                }

                var endIndent = indentStack.Count > 0
                    ? indentStack.Pop().EndIndent
                    : moduleStarted ? 1 : 0;
                output.Add(Pad(endIndent, trimmed, options));
                previousNonBlank = trimmed;
                continue;
            }

            if (IsLegacyClose(trimmed))
            {
                var closeIndent = indentStack.Count > 0
                    ? indentStack.Pop().EndIndent
                    : moduleStarted ? 1 : 0;
                output.Add(Pad(closeIndent, trimmed, options));
                previousNonBlank = trimmed;
                continue;
            }

            var indentUnits = ResolveContentIndent(indentStack, moduleStarted);
            output.Add(Pad(indentUnits, trimmed, options));

            if (trimmed.StartsWith("@", StringComparison.Ordinal))
            {
                moduleStarted = true;
                indentStack.Push(new BlockFrame(1, 1));
                previousNonBlank = trimmed;
                continue;
            }

            if (TryGetBlockShape(trimmed, out var shape))
            {
                indentStack.Push(shape switch
                {
                    BlockShape.BodyIndented => new BlockFrame(indentUnits, indentUnits + 1),
                    _ => new BlockFrame(indentUnits, indentUnits),
                });
            }
            else if (trimmed.Contains('{') && !trimmed.Contains('}'))
            {
                indentStack.Push(new BlockFrame(indentUnits, indentUnits + 1));
            }

            previousNonBlank = trimmed;
        }

        return string.Join('\n', output);
    }

    enum BlockShape
    {
        Container,
        BodyIndented,
    }

    static int ResolveContentIndent(Stack<BlockFrame> indentStack, bool moduleStarted)
    {
        if (indentStack.Count > 0)
        {
            return indentStack.Peek().ContentIndent;
        }

        return moduleStarted ? 1 : 0;
    }

    static string Pad(int indentUnits, string content, EditorConfig.EditorConfigOptions options)
    {
        if (indentUnits <= 0)
        {
            return content;
        }

        return string.Concat(Enumerable.Repeat(options.IndentUnit, indentUnits)) + content;
    }

    static bool IsEndLine(string trimmed) => trimmed.StartsWith("end ", StringComparison.OrdinalIgnoreCase);

    static bool IsLegacyClose(string trimmed) => trimmed is "}" || trimmed.StartsWith("}", StringComparison.Ordinal);

    static bool TryGetBlockShape(string trimmed, out BlockShape shape)
    {
        shape = BlockShape.Container;
        if (trimmed.StartsWith("@", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.StartsWith("layout grid", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("toolbar chrome", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("on click", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmed.StartsWith("card ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("tab ", StringComparison.OrdinalIgnoreCase))
        {
            shape = BlockShape.Container;
            return true;
        }

        var first = FirstToken(trimmed);
        if (first is null)
        {
            return false;
        }

        if (first.Equals("toolbar", StringComparison.OrdinalIgnoreCase))
        {
            shape = BlockShape.BodyIndented;
            return !trimmed.StartsWith("toolbar chrome", StringComparison.OrdinalIgnoreCase);
        }

        switch (first.ToLowerInvariant())
        {
            case "bind":
            case "filters":
            case "cards":
            case "views":
            case "data":
            case "transform":
            case "series":
            case "presentation":
                shape = BlockShape.BodyIndented;
                return true;
            case "runtime":
            case "configuration":
            case "wiring":
            case "report":
            case "extensions":
            case "page":
            case "group":
            case "manifest":
            case "grid":
            case "chrome":
            case "diagramlibrary":
                shape = BlockShape.Container;
                return true;
            default:
                return false;
        }
    }

    static string? FirstToken(string trimmed)
    {
        var span = trimmed.AsSpan();
        var i = 0;
        while (i < span.Length && char.IsWhiteSpace(span[i]))
        {
            i++;
        }

        if (i >= span.Length)
        {
            return null;
        }

        var start = i;
        while (i < span.Length && !char.IsWhiteSpace(span[i]))
        {
            i++;
        }

        return span[start..i].ToString();
    }
}
