namespace DashSpec.Modeling.Parse.DataFlow

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Lexing

/// <summary>Parse <c>{kind} flow … end {kind} flow</c> blocks (ADR-0093).</summary>
module QualifiedFlowBlockParser =

    let private legacyFlowMessage (contextLabel: string) =
        $"{contextLabel}: use qualified flow blocks (data flow, show flow, wire flow, action flow); bare 'flow' was removed."

    /// <summary>Peek whether the next tokens start a qualified flow header (<c>show flow</c>, …).</summary>
    let tryPeekQualifiedStart (reader: TokenReader) =
        match reader.TryPeekIdent() with
        | Some kw ->
            match FlowGraphKindRegistry.tryFindByKeyword kw with
            | Some kind ->
                let saved = reader.SavePosition()
                let ok = reader.TryKeyword kw && reader.TryKeyword "flow"
                reader.RestorePosition saved
                if ok then Some kind else None
            | None -> None
        | None -> None

    let consumeQualifiedStart (reader: TokenReader) =
        match tryPeekQualifiedStart reader with
        | Some kind ->
            reader.ReadIdent() |> ignore
            reader.ExpectKeyword "flow"
            kind
        | None -> raise (reader.Unexpected "qualified flow block (data flow, show flow, wire flow, action flow)")

    let tryRejectLegacyFlow (reader: TokenReader) (contextLabel: string) =
        if reader.TryKeyword "flow" then
            raise (DashSpecParseException(legacyFlowMessage contextLabel))

        ()

    let parseBlock
        (reader: TokenReader)
        (kind: FlowGraphKind)
        (scope: FlowGraphKindRegistry.FlowGraphScope)
        (contextLabel: string)
        =
        if not (FlowGraphKindRegistry.isAllowed scope kind) then
            raise (
                DashSpecParseException(
                    $"{contextLabel}: {FlowGraphKindRegistry.keyword kind} flow is not allowed in this scope."
                )
            )

        let endKind = FlowGraphKindRegistry.keyword kind
        let endId = FlowGraphKindRegistry.blockEndId kind
        let options = FlowLinkBlockParser.defaultOptions contextLabel
        let links = FlowLinkBlockParser.parseFlowBlock reader options endKind (Some endId)
        FlowGraphLinkRules.validateLinks contextLabel kind links
        links

    let tryParseAndAdd
        (reader: TokenReader)
        (builder: FlowGraphSections.Builder)
        (scope: FlowGraphKindRegistry.FlowGraphScope)
        (contextLabel: string)
        =
        match tryPeekQualifiedStart reader with
        | None -> false
        | Some kind ->
            reader.ReadIdent() |> ignore
            reader.ExpectKeyword "flow"
            let links = parseBlock reader kind scope contextLabel
            builder.Add(kind, links)
            true
