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

    /// <summary>Register imported units before report body parse (ADR-0098).</summary>
    let applyHeaderImports
        (header: ModuleHeader)
        (specDirectory: string option)
        (moduleKind: DocumentModuleKind)
        (includes: ModuleImportState)
        (parseOptions: DashSpecParseOptions)
        (diagnostics: ResizeArray<DashSpecDiagnostic>)
        =
        validateImports header.Imports

        if header.Imports.Count = 0 then ()
        else
            match specDirectory with
            | Some dir ->
                let index = UnitIndex.build dir

                for directive in header.Imports do
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
                                $"No module found for import {keyword} from '{directive.Namespace}'."
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
