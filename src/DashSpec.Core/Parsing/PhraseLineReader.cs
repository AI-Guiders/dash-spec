namespace DashSpec.Core.Parsing;

internal static class PhraseLineReader
{
    public static List<PhraseToken> ReadLineTokens(TokenReader reader)
    {
        reader.SkipNewlines();
        var tokens = new List<PhraseToken>();

        while (!reader.IsAt(TokenKind.Newline) &&
               !reader.IsAt(TokenKind.RBrace) &&
               !reader.IsEof)
        {
            switch (reader.CurrentKind)
            {
                case TokenKind.Ident:
                    tokens.Add(new PhraseToken(PhraseTokenKind.Ident, reader.ReadIdent()));
                    break;
                case TokenKind.String:
                    tokens.Add(new PhraseToken(PhraseTokenKind.String, reader.ReadString()));
                    break;
                case TokenKind.LParen:
                case TokenKind.RParen:
                case TokenKind.Comma:
                    reader.Advance();
                    break;
                default:
                    return tokens;
            }
        }

        return tokens;
    }
}
