namespace DashSpec.Modeling.Parse

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// <summary>Composable module-envelope extractor over the typed syntax tree (ADR-0130):
/// analogue of <c>BlockGrammar</c> for compilation units (<c>module &lt;kind&gt; &lt;id&gt;</c>).</summary>
module ModuleGrammar =

    type ModuleEnvelope =
        { Kind: DashSpecModuleKind
          Id: string
          Namespace: string option
          Imports: ImportDeclarationSyntax[]
          Body: DashSpecAstNode[]
          ModuleNode: ModuleDeclarationSyntax }

    let private directiveToKindAndId (directive: DashSpecModuleDirective) =
        match directive with
        | DashSpecModuleDirective.Dashboard id -> DashSpecModuleKind.Dashboard, id
        | DashSpecModuleDirective.Tab id -> DashSpecModuleKind.Section, id
        | DashSpecModuleDirective.Module(kind, id) -> kind, id

    let private kindMatches (expected: DashSpecModuleKind) (actual: DashSpecModuleKind) =
        match expected, actual with
        | DashSpecModuleKind.Flow _, DashSpecModuleKind.Flow _ -> true
        | _ -> expected = actual

    /// <summary>Extract the typed <see cref="ModuleEnvelope"/> of the expected module kind
    /// from a parsed <see cref="DashSpecAstNode"/> root, stripping header directives
    /// (<c>namespace</c>, <c>import</c>), blank lines, comment trivia, and the closing
    /// <c>end &lt;kind&gt;</c> so module compilers work directly on semantic body members.</summary>
    let unwrap (expectedKind: DashSpecModuleKind) (root: DashSpecAstNode) : ModuleEnvelope =
        let expectedKeyword = DashSpecModuleKind.toKeyword expectedKind

        let found =
            DashSpecAst.descendants root
            |> Seq.tryPick (function
                | DashSpecAstNode.ModuleDeclaration m ->
                    let kind, id = directiveToKindAndId m.Directive
                    if kindMatches expectedKind kind then
                        Some(m, kind, id)
                    else
                        raise (
                            DashSpecParseException(
                                $"Expected 'module {expectedKeyword} <id>', got 'module {DashSpecModuleKind.toKeyword kind} {id}'."))
                | _ -> None)

        match found with
        | None ->
            raise (DashSpecParseException($"Module requires 'module {expectedKeyword} <id>'."))
        | Some(m, actualKind, id) ->
            if String.IsNullOrWhiteSpace id then
                raise (DashSpecParseException($"'module {expectedKeyword}' requires an identifier."))

            let mutable ns: string option = None
            let imports = ResizeArray<ImportDeclarationSyntax>()
            let body = ResizeArray<DashSpecAstNode>()

            for child in m.Members do
                match child with
                | DashSpecAstNode.ClauseDeclaration c ->
                    match c.Kind with
                    | ClauseKind.NamespaceClause n ->
                        if ns.IsSome then
                            raise (DashSpecParseException("Module header allows only one 'namespace' directive."))
                        ns <- Some n
                    | _ -> body.Add child
                | DashSpecAstNode.ImportDeclaration imp ->
                    imports.Add imp
                | DashSpecAstNode.BlankLine _
                | DashSpecAstNode.EndBlock _ -> ()
                | c when DashSpecAst.isTrivia c -> ()
                | other -> body.Add other

            { Kind = actualKind
              Id = id
              Namespace = ns
              Imports = imports.ToArray()
              Body = body.ToArray()
              ModuleNode = m }
