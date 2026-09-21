namespace DashSpec.Modeling.Parse.Syntax

[<RequireQualifiedAccess>]
type DashSpecModuleDirective =
    | Dashboard of identifier: string
    | Tab of identifier: string

type DashSpecBlockOpener =
    | Named of keyword: DashSpecBlockKeyword * identifier: string
    | Anonymous of keyword: DashSpecBlockKeyword

/// Roslyn-style typed syntax tree for DashSpec block surface (planet SSOT).
[<RequireQualifiedAccess>]
type DashSpecAstNode =
    | CompilationUnit of CompilationUnitSyntax
    | ModuleDeclaration of ModuleDeclarationSyntax
    | BlockDeclaration of BlockDeclarationSyntax
    | CardReference of CardReferenceSyntax
    | EndBlock of EndBlockSyntax
    | Line of LineSyntax
    | BlankLine of BlankLineSyntax

and CompilationUnitSyntax =
    { Id: AstNodeId
      Span: TextSpan
      Members: DashSpecAstNode[] }

and ModuleDeclarationSyntax =
    { Id: AstNodeId
      Span: TextSpan
      Directive: DashSpecModuleDirective
      HeaderSpan: TextSpan
      Tokens: SyntaxToken[]
      Members: DashSpecAstNode[] }

and BlockDeclarationSyntax =
    { Id: AstNodeId
      Span: TextSpan
      Opener: DashSpecBlockOpener
      HeaderSpan: TextSpan
      Tokens: SyntaxToken[]
      Members: DashSpecAstNode[] }

and EndBlockSyntax =
    { Id: AstNodeId
      Span: TextSpan
      EndKeyword: DashSpecBlockKeyword
      EndId: string option
      Tokens: SyntaxToken[] }

and CardReferenceSyntax =
    { Id: AstNodeId
      Span: TextSpan
      CardId: string
      Tokens: SyntaxToken[] }

and LineSyntax =
    { Id: AstNodeId
      Span: TextSpan
      Tokens: SyntaxToken[] }

and BlankLineSyntax = { Id: AstNodeId; Span: TextSpan }

[<CLIMutable>]
type ParseTree =
    { Text: string
      Root: DashSpecAstNode
      Tokens: SyntaxToken[] }

/// DU discriminant for DashSpecAstNode (interop + diagnostics).
type DashSpecAstNodeKind =
    | CompilationUnit = 0
    | ModuleDeclaration = 1
    | BlockDeclaration = 2
    | CardReference = 3
    | EndBlock = 4
    | Line = 5
    | BlankLine = 6
