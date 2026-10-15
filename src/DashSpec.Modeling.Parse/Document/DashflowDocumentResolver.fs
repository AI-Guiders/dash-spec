namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module DashflowDocumentResolver =

    /// <summary>Resolve module data flow from an imported <c>flows</c> unit, selected by <c>connect { use flow &lt;symbol&gt; }</c> (ADR-0098).</summary>
    let resolve (shell: ReportCompileContext) : DashflowModule option * string option =
        match shell.FlowConnectSymbol, shell.Includes.DashflowModule with
        | None, included -> included, shell.Includes.DashflowPath
        | Some symbol, Some included when shell.Includes.FlowImportQualifiers.Contains symbol ->
            Some included, shell.Includes.DashflowPath
        | Some symbol, Some _ ->
            raise (
                DashSpecParseException(
                    $"connect {{ use flow '{symbol}' }} does not match any 'import flows' alias or namespace (ADR-0098)."
                )
            )
        | Some symbol, None ->
            raise (
                DashSpecParseException(
                    $"connect {{ use flow '{symbol}' }} requires 'import flows from <Namespace>' in the module header (ADR-0098)."
                )
            )
