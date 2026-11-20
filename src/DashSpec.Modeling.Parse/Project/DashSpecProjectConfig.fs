namespace DashSpec.Modeling.Parse.Project

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Syntax

/// <summary>Typed project configuration (<c>*.dsproj</c>, <c>module project &lt;id&gt;</c>) shared by LSP, host bootstrap, and compiler (ADR-0098 / ADR-0101).</summary>
type DashSpecProjectConfig =
    { ProjectRoot: string
      RootNamespace: string option
      /// <summary>Typed entry-point <c>module host &lt;id&gt;</c> reference (<c>host = &lt;id&gt;</c> or <c>use host &lt;id&gt;</c>).</summary>
      Host: DotSyntax option
      SourceRoots: IReadOnlyList<string>
      Files: IReadOnlyList<string>
      Exclude: IReadOnlyList<string>
      /// <summary>Typed implicit imports (<c>from &lt;Namespace&gt; import &lt;kind&gt;</c> or <c>from &lt;Namespace&gt; import { … }</c>):
      /// every module in the project sees these namespaces without a per-module import.</summary>
      ImplicitImports: IReadOnlyList<ImportDeclarationSyntax> }

    member this.HostId : string option =
        this.Host |> Option.map (fun h -> h.Text)

module DashSpecProjectConfig =

    let private trimQuotes (value: string) =
        let v = value.Trim()
        if v.Length >= 2 && v.[0] = '"' && v.[v.Length - 1] = '"' then v.Substring(1, v.Length - 2)
        else v

    let private extractStringItems (prop: PropertySyntax) : string list =
        match prop.Value with
        | PropertyValueSyntax.BracketList items
        | PropertyValueSyntax.CommaList items ->
            items
            |> Array.map (fun item -> trimQuotes item.Text)
            |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace s))
            |> Array.toList
        | PropertyValueSyntax.StringLiteral s when not (String.IsNullOrWhiteSpace s) ->
            [ s.Trim() ]
        | _ ->
            let inner =
                prop.ValueText.Trim()
                |> fun v ->
                    if v.StartsWith("[") && v.EndsWith("]") then v.Substring(1, v.Length - 2)
                    else v

            inner.Split(',', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun part -> trimQuotes (part.Trim()))
            |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace s))
            |> Array.toList

    /// <summary>AST-backed reader for <c>module project &lt;id&gt;</c> (<c>*.dsproj</c>).</summary>
    let parseText (projectRoot: string) (text: string) : DashSpecProjectConfig =
        let tree = SyntaxTree.parse text

        let mutable rootNamespace: string option = None
        let mutable hostModule: DotSyntax option = None
        let mutable sourceRoots = [ "." ]
        let files = ResizeArray<string>()
        let exclude = ResizeArray<string>()
        let implicitImports = ResizeArray<ImportDeclarationSyntax>()

        let applyContainer (view: UnitGrammar.ContainerView) (rawMembers: DashSpecAstNode[]) =
            for node in rawMembers do
                match node with
                | DashSpecAstNode.ImportDeclaration imp ->
                    implicitImports.Add imp
                | _ -> ()

            for clause in view.Clauses do
                match clause.Kind with
                | ClauseKind.NamespaceClause ns when rootNamespace.IsNone ->
                    rootNamespace <- Some ns
                | ClauseKind.UseClause(kind, target) when String.Equals(kind, "host", StringComparison.OrdinalIgnoreCase) ->
                    hostModule <- Some(DotSyntax.parse (trimQuotes target))
                | ClauseKind.DirectiveClause(kw, args)
                    when String.Equals(kw, "host", StringComparison.OrdinalIgnoreCase) && args.Length >= 1 ->
                    hostModule <- Some(DotSyntax.parse (trimQuotes args.[0]))
                | _ -> ()

            for prop in view.Properties do
                match prop.Key.Trim().ToLowerInvariant() with
                | "namespace"
                | "root_namespace" ->
                    rootNamespace <- Some(trimQuotes prop.ValueText)
                | "host" ->
                    let dot =
                        match prop.Value with
                        | PropertyValueSyntax.Member d -> d
                        | _ -> DotSyntax.parse (trimQuotes prop.ValueText)
                    if not (String.IsNullOrWhiteSpace dot.Text) then
                        hostModule <- Some dot
                | "source_roots" ->
                    let roots = extractStringItems prop
                    if not roots.IsEmpty then
                        sourceRoots <- roots
                | "files" ->
                    files.AddRange(extractStringItems prop)
                | "exclude" ->
                    exclude.AddRange(extractStringItems prop)
                | _ -> ()

        match UnitGrammar.tryUnwrap DashSpecModuleKind.Project tree.Root with
        | Some env ->
            rootNamespace <- env.Namespace
            implicitImports.AddRange env.Imports
            applyContainer env.View [||]
        | None ->
            let rootMembers = DashSpecAst.members tree.Root
            applyContainer (UnitGrammar.view rootMembers) rootMembers

        { ProjectRoot = projectRoot
          RootNamespace = rootNamespace
          Host = hostModule
          SourceRoots = sourceRoots :> IReadOnlyList<_>
          Files = files :> IReadOnlyList<_>
          Exclude = exclude :> IReadOnlyList<_>
          ImplicitImports = implicitImports :> IReadOnlyList<_> }

    let load (path: string) =
        if String.IsNullOrWhiteSpace path then invalidArg "path" "Path is required."
        if not (File.Exists path) then raise (FileNotFoundException($"Project file not found: '{path}'.", path))

        let projectRoot = Path.GetDirectoryName path |> Option.ofObj |> Option.defaultValue ""
        parseText projectRoot (File.ReadAllText path)

module DashSpecProjectLocator =

    /// <summary>Project file = `*.dsproj` (ADR-0101). Walks up parent directories from <paramref name="full"/> until a `.dsproj` is found.</summary>
    let rec private findProjectFile (full: string) =
        if String.IsNullOrWhiteSpace full || not (Directory.Exists full) then
            None
        else
            let direct =
                Directory.EnumerateFiles(full, "*.dsproj")
                |> Seq.sort
                |> Seq.tryHead

            match direct with
            | Some found -> Some found
            | None ->
                let parent = Path.GetDirectoryName full
                let root = Path.GetPathRoot full
                if String.IsNullOrWhiteSpace parent
                   || String.Equals(parent, full, StringComparison.OrdinalIgnoreCase)
                   || String.Equals(parent, root, StringComparison.OrdinalIgnoreCase) then
                    None
                else
                    findProjectFile parent

    let tryFindConfig (specDirectory: string option) =
        match specDirectory with
        | None | Some "" -> None
        | Some dir ->
            let full = Path.GetFullPath dir
            findProjectFile full |> Option.map DashSpecProjectConfig.load

    /// <summary>Resolve the `.dsproj` project root directory for a file or folder path (falls back to the directory itself when no `.dsproj` exists above it).</summary>
    let resolveProjectRoot (fileOrDirectoryPath: string) : string =
        if String.IsNullOrWhiteSpace fileOrDirectoryPath then
            "."
        else
            let full = Path.GetFullPath fileOrDirectoryPath
            let startDir =
                if Directory.Exists full then full
                else
                    let dir = Path.GetDirectoryName full
                    if String.IsNullOrWhiteSpace dir then "." else dir

            match findProjectFile startDir with
            | Some projFile ->
                Path.GetDirectoryName projFile
                |> Option.ofObj
                |> Option.defaultValue startDir
            | None -> startDir

/// <summary>Phase 2 project membership (ADR-0089): `files` positives minus `exclude` — the linkable universe, lex-sorted and deterministic.</summary>
module DashSpecProjectMembership =

    let private isGlob (entry: string) = entry.Contains('*') || entry.Contains('?')

    let private expandEntry (root: string) (entry: string) =
        let normalized = entry.Replace('\\', '/')

        if not (isGlob normalized) then
            [ Path.GetFullPath(Path.Combine(root, entry)) ]
        else
            let dirPart = Path.GetDirectoryName(normalized)
            let pattern = Path.GetFileName(normalized)

            let dir =
                Path.GetFullPath(Path.Combine(root, if String.IsNullOrEmpty dirPart then "." else dirPart))

            if Directory.Exists dir then
                Directory.EnumerateFiles(dir, pattern)
                |> Seq.map Path.GetFullPath
                |> Seq.sort
                |> Seq.toList
            else
                []

    let expand (cfg: DashSpecProjectConfig) =
        let positives =
            cfg.Files
            |> Seq.collect (expandEntry cfg.ProjectRoot)
            |> Seq.distinct
            |> Seq.sort

        let excluded =
            cfg.Exclude
            |> Seq.collect (expandEntry cfg.ProjectRoot)
            |> Set.ofSeq

        positives |> Seq.filter (fun p -> not (excluded.Contains p)) |> Seq.toList :> IReadOnlyList<string>

    /// <summary>True when membership is not declared (unconstrained) or the path is a declared member.</summary>
    let isDeclared (cfg: DashSpecProjectConfig) (path: string) =
        cfg.Files.Count = 0
        || (expand cfg |> Seq.contains (Path.GetFullPath path))
