namespace DashSpec.Modeling.Parse.DataFlow

open System.Collections.Generic
open DashSpec.Modeling.Core

/// <summary>Partitioned flow link sections keyed by <see cref="FlowGraphKind"/>.</summary>
[<CLIMutable>]
type FlowGraphSections =
    { Sections: IReadOnlyDictionary<FlowGraphKind, IReadOnlyList<FlowLinkDef>> }

module FlowGraphSections =

    let empty = { Sections = Dictionary<FlowGraphKind, IReadOnlyList<FlowLinkDef>>() }

    let tryGet (sections: FlowGraphSections) (kind: FlowGraphKind) =
        match sections.Sections.TryGetValue kind with
        | true, links -> Some links
        | false, _ -> None

    let linksFor (sections: FlowGraphSections) (kind: FlowGraphKind) =
        match tryGet sections kind with
        | Some links -> links
        | None -> [||] :> IReadOnlyList<_>

    let allLinks (sections: FlowGraphSections) =
        let ordered = ResizeArray<FlowLinkDef>()

        for kind in FlowGraphKindRegistry.allKinds do
            for link in linksFor sections kind do
                ordered.Add link

        ordered :> IReadOnlyList<_>

    let isEmpty (sections: FlowGraphSections) =
        sections.Sections.Count = 0

    type Builder() =
        let sections = Dictionary<FlowGraphKind, ResizeArray<FlowLinkDef>>()

        member _.Add(kind: FlowGraphKind, links: IReadOnlyList<FlowLinkDef>) =
            let bucket =
                match sections.TryGetValue kind with
                | true, existing -> existing
                | false, _ ->
                    let created = ResizeArray<FlowLinkDef>()
                    sections.[kind] <- created
                    created

            for link in links do
                bucket.Add link

        member _.ToSections() =
            if sections.Count = 0 then
                None
            else
                let immutable =
                    sections
                    |> Seq.map (fun kv -> kv.Key, kv.Value :> IReadOnlyList<_>)
                    |> dict
                    |> fun d -> Dictionary<FlowGraphKind, IReadOnlyList<FlowLinkDef>>(d)

                Some { Sections = immutable }
