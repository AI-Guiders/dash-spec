namespace DashSpec.Modeling.Parse.DataFlow

open System

/// <summary>Resolve dashflow path from card input back to source (ADR-0078).</summary>
module DashflowPathResolver =

    type CardInputRef =
        { Alias: string
          NodeId: string
          PortName: string }

    type ResolvedPath =
        { Source: DashflowSourceDef
          Transformers: DashflowTransformerDef list }

    let private portMatches (expected: string) (actual: string) =
        String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)

    let private resolveUpstreamPort (flow: DashflowModule) (inbound: FlowLinkDef) =
        match inbound.FromPort with
        | Some port when not (String.IsNullOrWhiteSpace port) -> port
        | _ ->
            let source =
                flow.Sources
                |> Seq.tryFind (fun s -> String.Equals(s.Id, inbound.FromNode, StringComparison.OrdinalIgnoreCase))

            match source with
            | Some s -> s.OutputPort
            | None ->
                let transformer =
                    flow.Transformers
                    |> Seq.tryFind (fun t -> String.Equals(t.Id, inbound.FromNode, StringComparison.OrdinalIgnoreCase))

                match transformer with
                | Some t -> defaultArg t.DefaultOutputPort ""
                | None -> ""

    let rec private walk (flow: DashflowModule) (nodeId: string) (portName: string) (transformers: DashflowTransformerDef list) =
        let source =
            flow.Sources
            |> Seq.tryFind (fun s -> String.Equals(s.Id, nodeId, StringComparison.OrdinalIgnoreCase))

        match source with
        | Some s ->
            if not (portMatches s.OutputPort portName) then
                raise (
                    InvalidOperationException(
                        $"Source '{nodeId}' has no output port '{portName}' (output is '{s.OutputPort}')."
                    )
                )

            { Source = s; Transformers = transformers }
        | None ->
            let transformer =
                flow.Transformers
                |> Seq.tryFind (fun t -> String.Equals(t.Id, nodeId, StringComparison.OrdinalIgnoreCase))
                |> Option.defaultWith (fun () ->
                    raise (InvalidOperationException($"Dashflow node '{nodeId}' was not found.")))

            if not (transformer.Outputs |> Seq.exists (fun (name, _) -> portMatches name portName)) then
                raise (InvalidOperationException($"Transformer '{nodeId}' has no output port '{portName}'."))

            let inboundLinks =
                flow.Links
                |> Seq.filter (fun l -> String.Equals(l.ToNode, nodeId, StringComparison.OrdinalIgnoreCase))
                |> Seq.toList

            if List.isEmpty inboundLinks then
                raise (
                    InvalidOperationException(
                        $"Transformer '{nodeId}' has no inbound links in dashflow '{flow.FlowId}'."
                    )
                )

            let inbound =
                if inboundLinks.Length = 1 then
                    inboundLinks.[0]
                else
                    inboundLinks
                    |> Seq.tryFind (fun l ->
                        match transformer.DefaultInputPort, l.ToPort with
                        | Some defaultPort, Some toPort when not (String.IsNullOrWhiteSpace defaultPort) ->
                            String.Equals(toPort, defaultPort, StringComparison.OrdinalIgnoreCase)
                        | _ -> false)
                    |> Option.defaultValue inboundLinks.[0]

            walk flow inbound.FromNode (resolveUpstreamPort flow inbound) (transformer :: transformers)

    let resolve (flow: DashflowModule) (input: CardInputRef) : ResolvedPath =
        try
            walk flow input.NodeId input.PortName []
        with
        | :? InvalidOperationException as ex when ex.Message.Contains("was not found") ->
            raise (
                InvalidOperationException(
                    $"Card input '{input.Alias}' references unknown dashflow node '{input.NodeId}'."
                )
            )
