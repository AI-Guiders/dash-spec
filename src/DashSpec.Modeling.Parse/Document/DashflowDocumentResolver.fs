namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module DashflowDocumentResolver =

    /// <summary>Resolve module data flow from an imported <c>flows</c> unit, selected by <c>connect { use flow &lt;symbol&gt; }</c> (ADR-0098).
    /// Managed views (ADR-0125) need no manifest: the materializer computes the
    /// deterministic view names from the parsed module itself.</summary>
    let resolve (shell: ReportCompileContext) : DashflowModule option * string option =
        match shell.FlowConnectSymbol, shell.Includes.DashflowModule with
        | None, Some included ->
            Some included, shell.Includes.DashflowPath
        | None, None -> None, shell.Includes.DashflowPath
        | Some symbol, Some included when shell.Includes.FlowImportQualifiers.Contains symbol ->
            Some included, shell.Includes.DashflowPath
        | Some symbol, Some _ ->
            raise (
                DashSpecParseException(
                    $"connect {{ use flow '{symbol}' }} does not match any 'import flows' alias or namespace."
                )
            )
        | Some symbol, None ->
            raise (
                DashSpecParseException(
                    $"connect {{ use flow '{symbol}' }} requires 'import flows from <Namespace>' in the module header."
                )
            )

    /// <summary>ADR-0127: synthesized row types ride the flow module into the
    /// document's catalog — one merge for every flow attach path (tab modules,
    /// dashboard-level <c>connect</c>, the card materializer alike). The merge
    /// itself is <c>TypeCatalog.withInferred</c>: the single tolerant merge in
    /// the codebase.</summary>
    let mergeInferredRowTypes
        (sourceLabel: string)
        (rowTypes: System.Collections.Generic.IReadOnlyDictionary<string, RowTypeDef>)
        (moduleFlow: DashflowModule option)
        : System.Collections.Generic.IReadOnlyDictionary<string, RowTypeDef> =
        match moduleFlow with
        | None -> rowTypes
        | Some flow when Array.isEmpty flow.InferredRowTypes -> rowTypes
        | Some flow ->
            let merged = TypeCatalog.withInferred sourceLabel flow.InferredRowTypes (TypeCatalog.ofDefinitions rowTypes.Values)
            merged.Definitions
