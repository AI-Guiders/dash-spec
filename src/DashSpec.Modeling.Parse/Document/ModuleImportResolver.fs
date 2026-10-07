namespace DashSpec.Modeling.Parse.Document

open System
open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>Bind phase for module <c>import</c> directives (ADR-0098). v1: validate header shape only.</summary>
module ModuleImportResolver =

    let private validateImports (imports: IReadOnlyList<ModuleImportDirective>) =
        let seen = HashSet<string>(StringComparer.OrdinalIgnoreCase)

        for directive in imports do
            let key = $"{ImportKindRegistry.keyword directive.Kind}|{directive.Namespace}"

            if not (seen.Add key) then
                raise (
                    DashSpec.Modeling.Core.DashSpecParseException(
                        $"Duplicate import of {ImportKindRegistry.keyword directive.Kind} from '{directive.Namespace}'."
                    )
                )

    let resolve (shell: ReportCompileContext) =
        if isNull (box shell) then invalidArg "shell" "Report compile context is required."

        validateImports shell.ModuleImports
