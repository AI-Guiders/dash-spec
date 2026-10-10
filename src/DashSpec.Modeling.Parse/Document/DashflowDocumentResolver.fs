namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module DashflowDocumentResolver =

    let private typeCatalog (shell: ReportCompileContext) =
        TypeCatalog.ofDefinitions (shell.Includes.ExportRowTypes().Values)

    /// <summary>Resolve module data flow from <c>connect { flow }</c>, <c>connect { use flow &lt;symbol&gt; }</c> (ADR-0098), or <c>!include</c> of a <c>.dashflow</c>.</summary>
    let resolve (shell: ReportCompileContext) : DashflowModule option * string option =
        match shell.Includes.DashflowModule, shell.FlowConnectPath, shell.FlowConnectSymbol with
        | _, Some _, Some _ ->
            raise (DashSpecParseException("Declare data flow either via connect { flow } or connect { use flow <symbol> }, not both."))
        | Some _, Some _, None ->
            raise (DashSpecParseException("Declare data flow either via connect { flow \"…\" } or !include of a .dashflow file, not both."))
        | Some included, None, None -> Some included, None
        | Some included, None, Some symbol ->
            if shell.Includes.FlowImportQualifiers.Contains symbol then
                Some included, None
            else
                raise (
                    DashSpecParseException(
                        $"connect {{ use flow '{symbol}' }} does not match any 'import flows' alias or namespace (ADR-0098)."
                    )
                )
        | None, None, Some symbol ->
            raise (
                DashSpecParseException(
                    $"connect {{ use flow '{symbol}' }} requires 'import flows from <Namespace>' in the module header (ADR-0098)."
                )
            )
        | None, None, None -> None, None
        | None, Some reference, None ->
            match shell.SpecDirectory with
            | None ->
                raise (DashSpecParseException("connect { flow \"…\" } requires specDirectory when parsing."))
            | Some specDirectory ->
                let catalog = typeCatalog shell
                let path, module' = DashflowResolver.parseReference reference specDirectory catalog
                Some module', Some path