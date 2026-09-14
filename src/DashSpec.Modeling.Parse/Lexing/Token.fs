namespace DashSpec.Modeling.Parse.Lexing

[<Struct>]
type Token =
    { Kind: TokenKind
      Value: string
      Start: int
      Length: int }

type ColumnBindingValue = { Column: string; Alias: string option }
