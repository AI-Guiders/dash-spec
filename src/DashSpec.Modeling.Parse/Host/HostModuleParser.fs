namespace DashSpec.Modeling.Parse.Host

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Layout
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Syntax

module HostModuleParser =

    let private unquote (raw: string) =
        let t = raw.Trim()
        if t.Length >= 2 && t.[0] = '"' && t.[t.Length - 1] = '"' then
            t.Substring(1, t.Length - 2)
        else
            t

    let private importShellLayout (specDirectory: string option) (sourceOffset: int) (namespaceName: string) =
        match specDirectory with
        | None
        | Some "" ->
            raise (DashSpecParseException("import layouts requires host file directory when parsing.", sourceOffset))
        | Some dir ->
            let index = DashSpec.Modeling.Parse.Document.UnitIndex.build dir

            let entries =
                index.Find namespaceName
                |> List.filter (fun entry ->
                    match entry.Kind with
                    | None -> true
                    | Some kind -> String.Equals(kind, "layout", StringComparison.OrdinalIgnoreCase)
                )

            match entries with
            | [ entry ] ->
                let board =
                    LayoutModuleParser.parseLayoutFile (
                        DashSpec.Modeling.Parse.Document.UnitSource.readUnit entry.Path
                    )

                LayoutModuleScopeValidator.ensureMatchesImportSite board LayoutScope.Host "import layouts"
                board
            | [] ->
                raise (DashSpecParseException($"No module found for import layouts from '{namespaceName}'.", sourceOffset))
            | _ ->
                raise (DashSpecParseException($"Host imports more than one layouts unit from '{namespaceName}'.", sourceOffset))

    let private parseBoolCell (id: string) (col: string) (defaultValue: bool) (raw: string option) =
        match raw with
        | None -> defaultValue
        | Some v when String.IsNullOrWhiteSpace v -> defaultValue
        | Some v ->
            match v.Trim().ToLowerInvariant() with
            | "true" -> true
            | "false" -> false
            | other ->
                raise (
                    DashSpecParseException(
                        $"links '{col}' for '{id}' must be true or false, got '{other}'."
                    )
                )

    let private linkDeclaration (addLink: HostLinkDefinition -> unit) : Construct.Declaration<HostLinkDefinition> =
        let cellsToLink (m: Map<string, string>) : HostLinkDefinition =
            match Construct.tryCell m "id" with
            | Some rawId when not (String.IsNullOrWhiteSpace rawId) ->
                let linkId = rawId.Trim()

                let url =
                    match Construct.tryCell m "url" with
                    | Some u when not (String.IsNullOrWhiteSpace u) -> unquote u
                    | _ -> raise (DashSpecParseException($"Host link '{linkId}' requires url."))

                let label =
                    match Construct.tryCell m "label" with
                    | Some l when not (String.IsNullOrWhiteSpace l) -> unquote l
                    | _ -> linkId

                let target =
                    match Construct.tryCell m "target" with
                    | Some t when not (String.IsNullOrWhiteSpace t) -> unquote t
                    | _ -> "_blank"

                { Id = linkId
                  Label = label
                  Url = url
                  Target = target
                  Topbar = parseBoolCell linkId "topbar" true (Construct.tryCell m "topbar")
                  Settings = parseBoolCell linkId "settings" true (Construct.tryCell m "settings") }
            | _ -> raise (DashSpecParseException("links row requires a non-empty 'id' cell."))

        { Name = "links"
          Vocabulary = [ "id"; "label"; "url"; "target"; "topbar"; "settings" ]
          CellsToRow = cellsToLink
          ConstructRow = addLink }

    let private projectLinksBlock (block: BlockDeclarationSyntax) (links: ResizeArray<HostLinkDefinition>) =
        let decl = linkDeclaration links.Add
        let view = UnitGrammar.view block.Members

        for child in view.Blocks do
            match child.Opener with
            | DashSpecBlockOpener.Named(DashSpecBlockKeyword.Link, linkId) ->
                let label = DashSpecAst.tryTitle (DashSpecAstNode.BlockDeclaration child) |> Option.defaultValue linkId
                let _ =
                    PropertyBlockParser.projectPropertiesFromAst
                        child.Members
                        PropertySchemas.hostLink
                        $"host link '{linkId}'"
                        false
                let form = Construct.fromAstBlock [ "id", linkId; "label", label ] child
                Construct.apply decl form
            | _ ->
                let headerText = child.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected block '{headerText}' in host links."))

        match Construct.tryFromAstTable block.Members with
        | Some tableForm -> Construct.apply decl tableForm
        | None -> ()

    let private projectSurfacesBlock (block: BlockDeclarationSyntax) (surfaces: ResizeArray<string>) =
        let view = UnitGrammar.view block.Members
        for node in view.Body do
            let tokens = DashSpecAst.tokens node |> Array.filter (fun t -> t.LexKind <> TokenKind.LineComment && t.LexKind <> TokenKind.BlockComment)
            match Array.toList tokens with
            | { LexKind = TokenKind.Ident; Text = kw } :: rest when String.Equals(kw, "show", StringComparison.OrdinalIgnoreCase) ->
                for tok in rest do
                    if tok.LexKind = TokenKind.Ident then
                        surfaces.Add tok.Text
                    elif tok.LexKind <> TokenKind.Comma then
                        raise (DashSpecParseException($"Unexpected token '{tok.Text}' in host surfaces."))
            | other ->
                let text = other |> List.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{text}' in host surfaces."))

    let parse (text: string) (specDirectory: string option) : HostDocument =
        if String.IsNullOrWhiteSpace text then invalidArg "text" "Host text is required."

        let tree = DashSpecSurfaceSyntax.parse text
        let env = UnitGrammar.unwrap DashSpecModuleKind.Host tree.Root
        let hostId = env.Id

        let mutable catalogPath = ""
        let configuration = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let presentation = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let hostSettings = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let links = ResizeArray<HostLinkDefinition>()
        let surfaces = ResizeArray<string>()
        let mutable shellLayout = None

        for imp in env.Imports do
            let kind = imp.Kind |> Option.defaultValue ""
            if not (String.Equals(kind, "layouts", StringComparison.OrdinalIgnoreCase)) then
                raise (DashSpecParseException($"@host module allows import layouts only, got import {kind}.", imp.Span.Start))
            shellLayout <- Some(importShellLayout specDirectory imp.Span.Start imp.Target)

        for node in env.View.Body do
            match node with
            | DashSpecAstNode.ClauseDeclaration c ->
                match c.Kind with
                | ClauseKind.DirectiveClause("catalog", args) when args.Length >= 1 ->
                    catalogPath <- unquote args.[0]
                | _ ->
                    let text = c.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected clause '{text}' in host '{hostId}'."))
            | DashSpecAstNode.BlockDeclaration b ->
                match b.Opener with
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Configuration ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostConfiguration "configuration" false do
                        configuration.[kv.Key] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Presentation ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostPresentation "presentation" false do
                        presentation.[kv.Key] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Menu ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostMenu "menu" false do
                        presentation.[$"menu_{kv.Key}"] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Host ->
                    for kv in PropertyBlockParser.projectPropertiesFromAst b.Members PropertySchemas.hostSettings "host" false do
                        hostSettings.[kv.Key] <- kv.Value
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Links ->
                    projectLinksBlock b links
                | DashSpecBlockOpener.Anonymous DashSpecBlockKeyword.Surfaces ->
                    projectSurfacesBlock b surfaces
                | _ ->
                    let headerText = b.Tokens |> Array.map (fun t -> t.Text) |> String.concat " "
                    raise (DashSpecParseException($"Unexpected block '{headerText}' in host '{hostId}'."))
            | other ->
                let text = DashSpecAst.tokens other |> Array.map (fun t -> t.Text) |> String.concat " "
                raise (DashSpecParseException($"Unexpected statement '{text}' in host '{hostId}'."))

        if String.IsNullOrWhiteSpace catalogPath then
            raise (DashSpecParseException($"@host '{hostId}' requires catalog \"path/to-catalog.ds\"."))

        { Id = hostId
          CatalogPath = catalogPath
          Configuration = configuration
          Presentation = presentation
          Host = hostSettings :> IReadOnlyDictionary<string, string>
          Links = links :> IReadOnlyList<_>
          Surfaces = surfaces :> IReadOnlyList<_>
          ShellLayout = shellLayout }

    let parseFile (path: string) : HostDocument =
        if String.IsNullOrWhiteSpace path then invalidArg "path" "Host file path is required."
        if not (File.Exists path) then
            raise (FileNotFoundException($"Host file not found: '{path}'.", path))
        let directory = Path.GetDirectoryName(Path.GetFullPath path)
        parse (File.ReadAllText path) (Some directory)

    do
        UnitProjector.register
            { KindKeyword = "host"
              ImportKind = None
              Role = UnitCompileRole.Root
              Project = fun ctx -> box (parse ctx.Text ctx.SpecDirectory) }
