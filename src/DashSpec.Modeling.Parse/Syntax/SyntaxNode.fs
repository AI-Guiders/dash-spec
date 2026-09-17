namespace DashSpec.Modeling.Parse.Syntax

type SyntaxNodeKind =
    | CompilationUnit = 0
    | ModuleDeclaration = 1
    | Block = 2
    | EndBlock = 3
    | Line = 4
    | BlankLine = 5

[<CLIMutable>]
type SyntaxNode =
    { Kind: SyntaxNodeKind
      Span: TextSpan
      Tokens: SyntaxToken[]
      Children: SyntaxNode[] }

[<CLIMutable>]
type ParseTree =
    { Text: string
      Root: SyntaxNode }


