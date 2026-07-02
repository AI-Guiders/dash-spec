namespace DashSpec.Core.Parsing;

/// <summary>Character scanner for .dashspec — keywords are recognized by the parser, not the lexer.</summary>
internal static class DashSpecLexer
{
    public static IReadOnlyList<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                if (text[i] is '\r' or '\n')
                {
                    tokens.Add(new Token(TokenKind.Newline, "\n", i, 0));
                    i++;
                    if (i < text.Length && text[i] is '\n' && text[i - 1] is '\r')
                    {
                        i++;
                    }

                    continue;
                }

                i++;
                continue;
            }

            if (text[i] is '#')
            {
                while (i < text.Length && text[i] is not '\r' and not '\n')
                {
                    i++;
                }

                continue;
            }

            var start = i;
            if (text[i] is '@')
            {
                tokens.Add(new Token(TokenKind.At, "@", start, 1));
                i++;
                continue;
            }

            if (text[i] is '{')
            {
                tokens.Add(new Token(TokenKind.LBrace, "{", start, 1));
                i++;
                continue;
            }

            if (text[i] is '}')
            {
                tokens.Add(new Token(TokenKind.RBrace, "}", start, 1));
                i++;
                continue;
            }

            if (text[i] is '=')
            {
                tokens.Add(new Token(TokenKind.Eq, "=", start, 1));
                i++;
                continue;
            }

            if (text[i] is '.')
            {
                if (i + 1 < text.Length && text[i + 1] is '.')
                {
                    tokens.Add(new Token(TokenKind.DotDot, "..", start, 2));
                    i += 2;
                    continue;
                }

                throw new DashSpecParseException($"Unexpected '.' at position {i}. Use '..' for date ranges.");
            }

            if (text[i] is '-')
            {
                var relStart = i;
                i++;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] is 'd' or 'D'))
                {
                    i++;
                }

                if (i <= relStart + 1)
                {
                    throw new DashSpecParseException(
                        $"Invalid relative day at position {relStart}. Use form -Nd, e.g. -7d.");
                }

                tokens.Add(new Token(TokenKind.RelativeDay, text[relStart..i], relStart, i - relStart));
                continue;
            }

            if (text[i] is ',')
            {
                tokens.Add(new Token(TokenKind.Comma, ",", start, 1));
                i++;
                continue;
            }

            if (text[i] is '"')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < text.Length && text[i] is not '"')
                {
                    if (text[i] is '\\' && i + 1 < text.Length)
                    {
                        i++;
                        sb.Append(text[i] is 'n' ? '\n' : text[i]);
                        i++;
                        continue;
                    }

                    sb.Append(text[i]);
                    i++;
                }

                if (i >= text.Length)
                {
                    throw new DashSpecParseException("Unterminated string literal.");
                }

                i++;
                tokens.Add(new Token(TokenKind.String, sb.ToString(), start, i - start));
                continue;
            }

            if (text[i] is '[')
            {
                if (i + 1 < text.Length && text[i + 1] is '[')
                {
                    var j = i + 2;
                    while (j < text.Length)
                    {
                        if (text[j] is ']' && j + 1 < text.Length && text[j + 1] is ']')
                        {
                            j += 2;
                            break;
                        }

                        j++;
                    }

                    if (j >= text.Length)
                    {
                        throw new DashSpecParseException("Unterminated [[ raw block.");
                    }

                    tokens.Add(new Token(TokenKind.Raw, text[start..j].Trim(), start, j - start));
                    i = j;
                    continue;
                }

                tokens.Add(new Token(TokenKind.LBracket, "[", start, 1));
                i++;
                continue;
            }

            if (text[i] is ']')
            {
                tokens.Add(new Token(TokenKind.RBracket, "]", start, 1));
                i++;
                continue;
            }

            if (IsIdentStart(text[i]))
            {
                i++;
                while (i < text.Length && IsIdentPart(text[i]))
                {
                    i++;
                }

                var value = text[start..i];
                tokens.Add(new Token(TokenKind.Ident, value, start, i - start));
                continue;
            }

            throw new DashSpecParseException($"Unexpected character '{text[i]}' at position {i}.");
        }

        tokens.Add(new Token(TokenKind.Eof, string.Empty, text.Length, 0));
        return tokens;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c is '_' or '.' || char.IsDigit(c);

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '.';
}
