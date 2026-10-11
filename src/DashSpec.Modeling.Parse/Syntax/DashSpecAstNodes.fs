namespace DashSpec.Modeling.Parse.Syntax

open AIGuiders.Platform.Modeling.Core.Identity

[<RequireQualifiedAccess>]
type DashSpecModuleDirective =
    | Dashboard of identifier: string
    | Tab of identifier: string
    /// <summary>Any other compilation-unit root: `module &lt;kind&gt; &lt;id&gt;` (diagram, layout, types, flow, presentation, tooltip, palette, catalog, host, include, transform) — ADR-0101.</summary>
    | Unit of keyword: string * identifier: string

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
    | ImportDeclaration of ImportDeclarationSyntax

and CompilationUnitSyntax =
    { Id: NodeId
      Span: TextSpan
      Members: DashSpecAstNode[] }

and ModuleDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      Directive: DashSpecModuleDirective
      HeaderSpan: TextSpan
      Tokens: SyntaxToken[]
      Members: DashSpecAstNode[] }

and BlockDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      Opener: DashSpecBlockOpener
      HeaderSpan: TextSpan
      Tokens: SyntaxToken[]
      Members: DashSpecAstNode[] }

and EndBlockSyntax =
    { Id: NodeId
      Span: TextSpan
      EndKeyword: DashSpecBlockKeyword
      EndId: string option
      Tokens: SyntaxToken[] }

and CardReferenceSyntax =
    { Id: NodeId
      Span: TextSpan
      CardId: string
      Tokens: SyntaxToken[] }

and LineSyntax =
    { Id: NodeId
      Span: TextSpan
      Tokens: SyntaxToken[] }

and BlankLineSyntax = { Id: NodeId; Span: TextSpan }

/// <summary>Module-header import edge: `import &lt;kind&gt; from &lt;namespace&gt; [as &lt;alias&gt;]` (ADR-0098) or legacy `!include "path"` / `using "path"` (ADR-0089).</summary>
and ImportDeclarationSyntax =
    { Id: NodeId
      Span: TextSpan
      /// `import` (canonical) or `include` (legacy path form).
      Keyword: string
      /// Unit kind keyword (types/flows/layouts/…) — None for the path form.
      Kind: string option
      /// Namespace (canonical) or file path/glob (legacy).
      Target: string
      Alias: string option
      Tokens: SyntaxToken[] }

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
    | ImportDeclaration = 7
