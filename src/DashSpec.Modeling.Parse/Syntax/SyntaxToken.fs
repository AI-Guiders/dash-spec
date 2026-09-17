namespace DashSpec.Modeling.Parse.Syntax

open DashSpec.Modeling.Parse.Lexing

type SyntaxToken =
    { Kind: DashSpecSyntaxKind
      LexKind: TokenKind
      Text: string
      Span: TextSpan }
