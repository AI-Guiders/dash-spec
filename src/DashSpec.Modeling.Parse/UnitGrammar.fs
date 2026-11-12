namespace DashSpec.Modeling.Parse

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// <summary>Unified container &amp; unit grammar over the typed syntax tree (ADR-0130):
/// both <c>module &lt;kind&gt; &lt;id&gt;</c> and <c>&lt;block&gt; [&lt;id&gt;]</c> share the same
/// <see cref="ContainerView"/> over <see cref="DashSpecAstNode"/> members.</summary>
module UnitGrammar =

    /// Structured projection of any container's (`ModuleDeclaration` or `BlockDeclaration`) body members.
    type ContainerView =
        { Properties: PropertySyntax[]
          Clauses: ClauseSyntax[]
          Blocks: BlockDeclarationSyntax[]
          GridRows: GridRowSyntax[]
          TableRows: TableRowSyntax[]
          FlowLinks: FlowLinkSyntax[]
          TypeFields: TypeFieldSyntax[]
          Ports: PortDeclarationSyntax[]
          Body: DashSpecAstNode[] }

    type UnitEnvelope =
        { Kind: DashSpecModuleKind
          Id: string
          Namespace: string option
          Imports: ImportDeclarationSyntax[]
          Body: DashSpecAstNode[]
          View: ContainerView
          ModuleNode: ModuleDeclarationSyntax }

    /// Build a <see cref="ContainerView"/> from any container's member array (unit or block).
    let view (members: DashSpecAstNode[]) : ContainerView =
        let properties = ResizeArray<PropertySyntax>()
        let clauses = ResizeArray<ClauseSyntax>()
        let blocks = ResizeArray<BlockDeclarationSyntax>()
        let gridRows = ResizeArray<GridRowSyntax>()
        let tableRows = ResizeArray<TableRowSyntax>()
        let flowLinks = ResizeArray<FlowLinkSyntax>()
        let typeFields = ResizeArray<TypeFieldSyntax>()
        let ports = ResizeArray<PortDeclarationSyntax>()
        let body = ResizeArray<DashSpecAstNode>()

        for node in members do
            match node with
            | DashSpecAstNode.BlankLine _
            | DashSpecAstNode.EndBlock _ -> ()
            | n when DashSpecAst.isTrivia n -> ()
            | DashSpecAstNode.PropertyAssignment p ->
                properties.Add p
                body.Add node
            | DashSpecAstNode.ClauseDeclaration c ->
                clauses.Add c
                body.Add node
            | DashSpecAstNode.BlockDeclaration b ->
                blocks.Add b
                body.Add node
            | DashSpecAstNode.GridRowDeclaration g ->
                gridRows.Add g
                body.Add node
            | DashSpecAstNode.TableRowDeclaration t ->
                tableRows.Add t
                body.Add node
            | DashSpecAstNode.FlowLinkDeclaration l ->
                flowLinks.Add l
                body.Add node
            | DashSpecAstNode.TypeFieldDeclaration f ->
                typeFields.Add f
                body.Add node
            | DashSpecAstNode.PortDeclaration p ->
                ports.Add p
                body.Add node
            | other ->
                body.Add other

        { Properties = properties.ToArray()
          Clauses = clauses.ToArray()
          Blocks = blocks.ToArray()
          GridRows = gridRows.ToArray()
          TableRows = tableRows.ToArray()
          FlowLinks = flowLinks.ToArray()
          TypeFields = typeFields.ToArray()
          Ports = ports.ToArray()
          Body = body.ToArray() }

    /// If a unit body wraps its members in an optional inner block of the given keyword
    /// (e.g. `presentation … end presentation` inside `module presentation`, `transform series` inside `module transform`,
    /// or `palette … end palette` inside `module palette`), flatten that inner block's members into the body stream.
    let unwrapOptionalBlock (keyword: DashSpecBlockKeyword) (members: DashSpecAstNode[]) : DashSpecAstNode[] =
        members
        |> Array.collect (function
            | DashSpecAstNode.BlockDeclaration b when b.Opener = DashSpecBlockOpener.Anonymous keyword ->
                b.Members
            | other -> [| other |])

    let private directiveToKindAndId (directive: DashSpecModuleDirective) =
        match directive with
        | DashSpecModuleDirective.Dashboard id -> DashSpecModuleKind.Dashboard, id
        | DashSpecModuleDirective.Tab id -> DashSpecModuleKind.Section, id
        | DashSpecModuleDirective.Module(kind, id) -> kind, id

    let private kindMatches (expected: DashSpecModuleKind) (actual: DashSpecModuleKind) =
        match expected, actual with
        | DashSpecModuleKind.Flow _, DashSpecModuleKind.Flow _ -> true
        | _ -> expected = actual

    let private buildEnvelope (m: ModuleDeclarationSyntax) (actualKind: DashSpecModuleKind) (id: string) : UnitEnvelope =
        if String.IsNullOrWhiteSpace id then
            let kw = DashSpecModuleKind.toKeyword actualKind
            raise (DashSpecParseException($"'module {kw}' requires an identifier."))

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

        let bodyArr = body.ToArray()

        { Kind = actualKind
          Id = id
          Namespace = ns
          Imports = imports.ToArray()
          Body = bodyArr
          View = view bodyArr
          ModuleNode = m }

    /// Extract the <see cref="UnitEnvelope"/> of any unit in the syntax tree without knowing its kind in advance.
    let tryUnwrapAny (root: DashSpecAstNode) : UnitEnvelope option =
        DashSpecAst.descendants root
        |> Seq.tryPick (function
            | DashSpecAstNode.ModuleDeclaration m ->
                let kind, id = directiveToKindAndId m.Directive
                Some(buildEnvelope m kind id)
            | _ -> None)

    /// Extract the typed <see cref="UnitEnvelope"/> of the expected unit kind from a parsed <see cref="DashSpecAstNode"/> root.
    let unwrap (expectedKind: DashSpecModuleKind) (root: DashSpecAstNode) : UnitEnvelope =
        let expectedKeyword = DashSpecModuleKind.toKeyword expectedKind

        match tryUnwrapAny root with
        | None ->
            raise (DashSpecParseException($"Module requires 'module {expectedKeyword} <id>'."))
        | Some env ->
            if kindMatches expectedKind env.Kind then
                env
            else
                raise (
                    DashSpecParseException(
                        $"Expected 'module {expectedKeyword} <id>', got 'module {DashSpecModuleKind.toKeyword env.Kind} {env.Id}'."))

    /// Non-throwing variant of <see cref="unwrap"/> for speculative classification.
    let tryUnwrap (expectedKind: DashSpecModuleKind) (root: DashSpecAstNode) : UnitEnvelope option =
        try
            Some(unwrap expectedKind root)
        with _ ->
            None
