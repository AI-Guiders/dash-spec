namespace DashSpec.Modeling.CodeCenter

open System
open AIGuiders.Platform.Modeling.CodeCenter

/// Planet-owned projection classification for DashSpec outline labels (ADR-0067 ProjectionHints).
module DashSpecProjectionHints =

    let private tryKeyword (label: string) =
        if String.IsNullOrWhiteSpace label then
            None
        elif label.StartsWith("@", StringComparison.Ordinal) then
            Some "module"
        else
            match label.IndexOf(' ') with
            | -1 -> Some label
            | index -> Some(label.Substring(0, index))

    let isDiagramBox (name: string) =
        match tryKeyword name with
        | Some "module"
        | Some "tab"
        | Some "card"
        | Some "page"
        | Some "phase"
        | Some "group"
        | Some "cards"
        | Some "views"
        | Some "layout"
        | Some "chrome"
        | Some "diagram" -> true
        | _ -> false

    let isFormField (name: string) =
        match tryKeyword name with
        | Some "data"
        | Some "filter"
        | Some "series"
        | Some "datasource"
        | Some "transform"
        | Some "variables"
        | Some "presentation"
        | Some "wiring"
        | Some "runtime"
        | Some "configuration"
        | Some "report"
        | Some "bind"
        | Some "import" -> true
        | _ -> false

    let formatPreviewLabel (name: string) =
        match tryKeyword name with
        | Some keyword -> $"[{keyword}] {name}"
        | None -> name

    let availableProjections () : ProjectionDescriptor list =
        List.append
            (ProjectionDescriptor.defaultAvailable ())
            [ { Kind = DocumentProjectionKind.Diagram
                PluginId = "dashspec.diagram"
                NodeId = None
                Dialect = None }
              { Kind = DocumentProjectionKind.Form
                PluginId = "dashspec.form"
                NodeId = None
                Dialect = None }
              { Kind = DocumentProjectionKind.Preview
                PluginId = "dashspec.preview"
                NodeId = None
                Dialect = None } ]

module DashSpecProjectionBridge =

    let isDiagramBox (name: string) = DashSpecProjectionHints.isDiagramBox name

    let isFormField (name: string) = DashSpecProjectionHints.isFormField name

    let availableProjections () = DashSpecProjectionHints.availableProjections ()

    let buildPreviewOutline (snapshot: DocumentSnapshot) =
        let nodes = DocumentGraph.listNodes snapshot

        if List.isEmpty nodes then
            String.Empty
        else
            let byId =
                nodes |> List.map (fun node -> node.Id, node) |> Map.ofList

            let depth (nodeId, node) =
                let rec walk id depth =
                    match Map.tryFind id byId with
                    | None -> depth
                    | Some n ->
                        match n.Parent with
                        | None -> depth
                        | Some parentId -> walk parentId (depth + 1)

                walk nodeId 0

            nodes
            |> List.sortBy (fun node -> node.Range.Start)
            |> List.map (fun node ->
                let indent = depth (node.Id, node)
                $"{String(' ', indent * 2)}{DashSpecProjectionHints.formatPreviewLabel node.Name}")
            |> String.concat Environment.NewLine

    let buildPreviewOutlineFromText (text: string) =
        buildPreviewOutline (DashSpecDocumentGraph.rebuildFromText text)
