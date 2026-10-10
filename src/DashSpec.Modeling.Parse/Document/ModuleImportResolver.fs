namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Include
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

    /// <summary>Register imported units before report body parse (replaces envelope <c>!include</c> when indexed).</summary>
    let applyHeaderImports
        (header: ModuleHeader)
        (specDirectory: string option)
        (moduleKind: DocumentModuleKind)
        (includes: ModuleIncludeState)
        (parseOptions: DashSpecParseOptions)
        (diagnostics: ResizeArray<DashSpecDiagnostic>)
        =
        validateImports header.Imports

        if header.Imports.Count = 0 then ()
        else
            match specDirectory, DashSpecProjectLocator.tryFindConfig specDirectory with
            | Some dir, Some project ->
                for directive in header.Imports do
                    let paths =
                        DashSpecProjectIndex.collectPathsForImport
                            project
                            dir
                            directive.Kind
                            directive.Namespace
                            parseOptions.TolerateIncompleteIncludes

                    for path in paths do
                        IncludeExpander.registerUnitFile
                            path
                            dir
                            moduleKind
                            includes
                            parseOptions.TolerateIncompleteIncludes

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
                        "Module 'import' requires specDirectory and dashspec.toml with [[index]] entries for each namespace."
                    )
                )

    let resolve (shell: ReportCompileContext) =
        if isNull (box shell) then invalidArg "shell" "Report compile context is required."

        validateImports shell.ModuleImports
