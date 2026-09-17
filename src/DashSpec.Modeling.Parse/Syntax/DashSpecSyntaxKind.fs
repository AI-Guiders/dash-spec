namespace DashSpec.Modeling.Parse.Syntax

type DashSpecSyntaxKind =
    | Comment = 0
    | ModuleHeader = 1
    | Keyword = 2
    | EndKeyword = 3
    | String = 4
    | Number = 5
    | Operator = 6
    | Identifier = 7
    | Punctuation = 8

[<CLIMutable>]
type DashSpecSyntaxSpan =
    { Start: int
      Length: int
      Kind: DashSpecSyntaxKind }
