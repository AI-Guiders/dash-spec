namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow

module DashflowDocumentResolver =

    /// <summary>Resolve module data flow from an imported <c>flows</c> unit, selected by <c>connect { use flow &lt;symbol&gt; }</c> (ADR-0098).
    /// A view-marked module (ADR-0125) carries its managed-views manifest: loaded
    /// here from <c>dashspec.views.json</c> next to the corpus when the directory is
    /// known, so the materializer can point cards at the physical views.</summary>
    let private withManifest (shell: ReportCompileContext) (flow: DashflowModule) =
        if not flow.EmitViews then
            flow
        else
            match shell.SpecDirectory with
            | Some directory when not (String.IsNullOrWhiteSpace directory) ->
                { flow with ViewManifest = DashflowViewManifest.tryLoad directory }
            | _ -> flow

    let resolve (shell: ReportCompileContext) : DashflowModule option * string option =
        match shell.FlowConnectSymbol, shell.Includes.DashflowModule with
        | None, Some included ->
            Some(withManifest shell included), shell.Includes.DashflowPath
        | None, None -> None, shell.Includes.DashflowPath
        | Some symbol, Some included when shell.Includes.FlowImportQualifiers.Contains symbol ->
            Some(withManifest shell included), shell.Includes.DashflowPath
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
