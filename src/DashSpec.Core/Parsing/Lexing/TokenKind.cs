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
    Ident,
    String,
    Raw,
    Newline,
    Eof,
}
