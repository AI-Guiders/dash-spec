namespace DashSpec.Core.Parsing;

internal static class ParserUtilities
{
    public static TokenReader CreateReader(string text)
    {
        var tokens = DashSpecLexer.Tokenize(text);
        return new TokenReader(tokens);
    }

    public static IReadOnlyList<string> ParseFilterPlacementList(TokenReader reader, string blockName)
    {
        if (reader.IsAt(TokenKind.LBrace))
        {
            return PropertyBlockParser.ParseCommaListBlock(reader, blockName);
        }

        return reader.ReadCommaListInline();
    }

    /// <summary>Reads optional <c>ref &lt;id&gt;</c> postfix without crossing a newline.</summary>
    public static string? TryReadLayoutRef(TokenReader reader)
    {
        if (!reader.TryKeywordSameLine("ref"))
        {
            return null;
        }

        return reader.ReadIdentSameLine();
    }
}
