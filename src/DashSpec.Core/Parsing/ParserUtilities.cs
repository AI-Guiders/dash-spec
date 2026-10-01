using DashSpec.Modeling.Parse.Lexing;

namespace DashSpec.Core.Parsing;

internal static class ParserUtilities
{
    public static TokenReader CreateReader(string text) =>
        new(Modeling.Parse.Lexing.DashSpecLexer.tokenize(text), sourceText: text);

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
