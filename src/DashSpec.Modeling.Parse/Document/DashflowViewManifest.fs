namespace DashSpec.Modeling.Parse.Document

open System
open System.IO
open System.Text.Json

/// <summary>
/// ADR-0125: the managed-views manifest (<c>dashspec.views.json</c>) — the compiled
/// artifact that ships with the catalog, the same symmetry as TypedRows.dll
/// (ADR-0124). Written by <c>dsc build views</c>; read by the compile pipeline: a
/// card whose flow module is view-marked reads the physical view by its
/// deterministic content-addressed name instead of assembling the query from the
/// graph at runtime.
/// </summary>
module DashflowViewManifest =

    /// One emitted physical view: chain terminal node → view name (+ its row type).
    type DashflowViewEntry =
        { Flow: string
          Chain: string
          ViewName: string
          RowType: string
          IsFunction: bool }

    let ManifestFileName = "dashspec.views.json"

    let private options = JsonSerializerOptions(JsonSerializerDefaults.Web)

    /// <summary>Load the manifest next to a corpus; a missing file is an empty
    /// manifest (views were never built — the reader falls back to graph queries).</summary>
    let tryLoad (specDirectory: string) : DashflowViewEntry list =
        let path = Path.Combine(specDirectory, ManifestFileName)

        if not (File.Exists path) then
            []
        else
            try
                JsonSerializer.Deserialize<DashflowViewEntry[]>(File.ReadAllText path, options)
                |> Array.toList
            with :? JsonException ->
                // A corrupt manifest never breaks reading — the graph path is the fallback.
                []

    /// <summary>The view for a card's flow input: the terminal node id IS the chain id.</summary>
    let tryFind (views: DashflowViewEntry list) (flowId: string) (nodeId: string) =
        views
        |> List.tryFind (fun v ->
            String.Equals(v.Flow, flowId, StringComparison.OrdinalIgnoreCase)
            && String.Equals(v.Chain, nodeId, StringComparison.OrdinalIgnoreCase))
