namespace DashSpec.Core.Parsing;

internal enum TokenKind
{
    At,
    Bang,
    LBrace,
    RBrace,
    Eq,
    DotDot,
    RelativeDay,
    Comma,
    LBracket,
    RBracket,
    LParen,
    RParen,
    Ident,
    String,
    Raw,
    Newline,
    Eof,
}
