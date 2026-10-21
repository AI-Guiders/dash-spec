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
    | TableRow
    | FlowArrow
    | LBracket
    | RBracket
    /// <summary>Empty grid cell — <c>[]</c> as a whole token ([ADR-0111] §2: the grid is drawn with empty cells).</summary>
    | EmptyCell
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

