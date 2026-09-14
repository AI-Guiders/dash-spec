namespace DashSpec.Modeling.Parse.Lexing

type TokenKind =
    | At
    | Bang
    | LBrace
    | RBrace
    | Eq
    | DotDot
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
    | Newline
    | Eof
