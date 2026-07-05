using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class ExtensionBlockParser
{
    public static ExtensionBlockNode Parse(
        TokenReader reader,
        string keyword,
        IReadOnlySet<string> allowedTopLevelKeywords)
    {
        if (!allowedTopLevelKeywords.Contains(keyword))
        {
            throw new DashSpecParseException($"Unknown extension block '{keyword}'.");
        }

        var actual = reader.ReadIdent();
        if (!string.Equals(actual, keyword, StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException($"Expected extension block '{keyword}', got '{actual}'.");
        }

        return ParseBlock(reader, keyword);
    }

    internal static ExtensionBlockNode ParseBlock(TokenReader reader, string keyword)
    {
        reader.Expect(TokenKind.LBrace);
        reader.SkipNewlines();

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nested = new List<ExtensionBlockNode>();

        while (!reader.IsAt(TokenKind.RBrace) && !reader.IsEof)
        {
            reader.SkipNewlines();
            if (reader.IsAt(TokenKind.RBrace))
            {
                break;
            }

            var mark = reader.CreateMark();
            var first = reader.ReadIdent();
            reader.SkipNewlines();

            if (reader.IsAt(TokenKind.Eq))
            {
                reader.Expect(TokenKind.Eq);
                properties[first] = reader.ReadScalarValue();
                reader.SkipNewlines();
                continue;
            }

            if (reader.IsAt(TokenKind.LBrace))
            {
                reader.Rewind(mark);
                var childKeyword = reader.ReadIdent();
                nested.Add(ParseBlock(reader, childKeyword));
                reader.SkipNewlines();
                continue;
            }

            var tail = reader.ReadRestOfLine();
            properties[first] = string.IsNullOrWhiteSpace(tail) ? first : $"{first} {tail}".Trim();
            reader.SkipNewlines();
        }

        reader.Expect(TokenKind.RBrace);
        return new ExtensionBlockNode(keyword, properties, nested);
    }
}
