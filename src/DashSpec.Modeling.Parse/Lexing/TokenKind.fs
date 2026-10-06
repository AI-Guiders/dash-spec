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
    /// <summary>Fixed UTC civil offset literal (<c>UTC</c>, <c>UTC+3</c>, <c>UTC+03:30</c>) for dashflow <c>to_zone</c>.</summary>
    | TimeShift
    /// <summary>IANA tz id (<c>Europe/Moscow</c>, <c>America/New_York</c>, …) — converted to TimeShift at resolve.</summary>
    | IanaZone
    | Comma
    | Colon
    | FlowArrow
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

