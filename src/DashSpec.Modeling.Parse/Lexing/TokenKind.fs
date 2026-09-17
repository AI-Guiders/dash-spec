namespace DashSpec.Modeling.Parse.Lexing

type TokenKind =
    | At
    | Bang
    | LBrace
    | RBrace
    | Eq
    | DotDot
    | Dot
    | Slash
    | RelativeDay
    | Comma
    | LBracket
    | RBracket
    | LParen
    | RParen
    | Ident
    | String
    | HexColor
    | Raw
    | LineComment
    | BlockComment
    | Newline
    | Eof

