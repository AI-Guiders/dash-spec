namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module DashflowDocumentResolver =

    let private typeCatalog (shell: DashboardShellContext) =
        TypeCatalog.ofDefinitions (shell.Includes.ExportRowTypes().Values)

    /// <summary>Resolve module data flow from <c>wiring { flow }</c> or <c>!include</c> of <c>.dashflow</c>.</summary>
    let resolve (shell: DashboardShellContext) : DashflowModule option * string option =
        match shell.Includes.DashflowModule, shell.FlowWiringPath, shell.SpecDirectory with
        | Some _, Some _, _ ->
            raise (DashSpecParseException("Declare data flow either via wiring { flow \"…\" } or !include of a .dashflow file, not both."))
        | Some included, None, _ -> Some included, None
        | None, None, _ -> None, None
        | None, Some _, None ->
            raise (DashSpecParseException("wiring { flow \"…\" } requires specDirectory when parsing."))
        | None, Some reference, Some specDirectory ->
            let catalog = typeCatalog shell
            let path, module' = DashflowResolver.parseReference reference specDirectory catalog
            Some module', Some path
