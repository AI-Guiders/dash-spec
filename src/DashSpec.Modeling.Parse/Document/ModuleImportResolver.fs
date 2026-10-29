namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open System.IO
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Import
open DashSpec.Modeling.Parse.Lexing
open DashSpec.Modeling.Parse.Project

/// <summary>Bind phase for module <c>import</c> directives (ADR-0098).</summary>
module ModuleImportResolver =

    let private validateImports (imports: IReadOnlyList<ModuleImportDirective>) =
        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        for directive in imports do
            let key = $"{ImportKindRegistry.keyword directive.Kind}|{directive.Namespace}"

            if not (seen.Add key) then
                raise (
                    DashSpecParseException(
                        $"Duplicate import of {ImportKindRegistry.keyword directive.Kind} from '{directive.Namespace}'."
                    )
                )

    let warnMissingNamespace (diagnostics: ResizeArray<DashSpecDiagnostic>) =
        diagnostics.Add(
            DashSpecDiagnostic.create
                0
                0
                0
                0
                "Module omits 'namespace'; path-derived namespaces are deprecated (ADR-0098)."
                DashSpecDiagnosticSeverity.Warning
        )

    /// <summary>
    /// Implicit imports (`.dsproj` <c>global import</c> lines, like C# ImplicitUsings):
    /// every module in the project sees these namespaces without a per-module import.
    /// Already-imported namespaces are skipped.
    /// </summary>
    let private appendImplicitImports
        (header: ModuleHeader)
        (specDirectory: string option)
        =
        match specDirectory with
        | Some dir ->
            match DashSpecProjectLocator.tryFindConfig (Some dir) with
            | Some cfg when cfg.ImplicitImports.Count > 0 ->
                let headerKeys =
                    header.Imports
                    |> Seq.map (fun d -> $"{ImportKindRegistry.keyword d.Kind}|{d.Namespace}")
                    |> HashSet<string>

                cfg.ImplicitImports
                |> Seq.choose (fun line ->
                    // "presentations from DashSpec.Std" → directive
                    let parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    match parts with
                    | [| kindKeyword; "from"; ns |] ->
                        match ImportKindRegistry.tryFindByKeyword kindKeyword with
                        | Some kind ->
                            let key = $"{ImportKindRegistry.keyword kind}|{ns}"
                            if not (headerKeys.Contains key) then
                                Some { Kind = kind; Namespace = ns; Alias = None; SourceOffset = 0 }
                            else None
                        | None -> None
                    | _ -> None)
                |> Seq.toList
            | _ -> []
        | None -> []

    /// <summary>Register imported units before report body parse (ADR-0098).</summary>
    let applyHeaderImports
        (header: ModuleHeader)
        (specDirectory: string option)
        (moduleKind: DocumentModuleKind)
        (includes: ModuleImportState)
        (diagnostics: ResizeArray<DashSpecDiagnostic>)
        =
        validateImports header.Imports

        let implicitImports = appendImplicitImports header specDirectory
        let allImports : IReadOnlyList<ModuleImportDirective> =
            if List.isEmpty implicitImports then header.Imports
            else (header.Imports |> Seq.toList) @ implicitImports

        if allImports.Count = 0 then ()
        else
            match specDirectory with
            | Some dir ->
                let index = UnitIndex.build dir

                for directive in allImports do
                    let keyword = ImportKindRegistry.keyword directive.Kind
                    let unitKeyword = ImportKindRegistry.moduleKeyword directive.Kind

                    let entries =
                        index.Find directive.Namespace
                        |> List.filter (fun entry ->
                            match entry.Kind with
                            | None -> true
                            | Some kind -> String.Equals(kind, unitKeyword, StringComparison.OrdinalIgnoreCase)
                        )

                    if List.isEmpty entries then
                        raise (
                            DashSpecParseException(
                                $"No module found for import {keyword} from '{directive.Namespace}'.",
                                directive.SourceOffset
                            )
                        )

                    for entry in entries do
                        UnitImport.registerUnitFile entry.Path moduleKind includes (Some directive.Kind)

                    if directive.Kind = ImportKind.Flows then
                        let namespaceName = directive.Namespace
                        includes.RegisterFlowImportQualifier namespaceName

                        let lastSegment =
                            namespaceName.Split('.') |> Array.last

                        includes.RegisterFlowImportQualifier lastSegment

                        match directive.Alias with
                        | Some alias -> includes.RegisterFlowImportQualifier alias
                        | None -> ()
            | _ ->
                raise (
                    DashSpecParseException(
                        "Module 'import' requires specDirectory (set via runtime manifest)."
                    )
                )

    let resolve (shell: ReportCompileContext) =
        if isNull (box shell) then invalidArg "shell" "Report compile context is required."

        validateImports shell.ModuleImports
