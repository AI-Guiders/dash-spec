namespace DashSpec.Core.Parsing;

internal enum TokenKind
{
    At,
    LBrace,
    RBrace,
    Eq,
    DotDot,
    RelativeDay,
    Comma,
    LBracket,
    RBracket,
    Ident,
    String,
    Raw,
    Newline,
    Eof,
}
