namespace DashSpec.Modeling.Parse.DataFlow

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.Lexing

/// <summary>Shared flow link lines inside balanced blocks (ADR-0093 qualified end labels).</summary>
module FlowLinkBlockParser =

    type ParseOptions =
        { ContextLabel: string
          ForbiddenKeywords: IReadOnlySet<string> }

    let private defaultForbidden =
        [| "bind"; "slot" |]
        |> Set.ofArray
        |> fun s -> HashSet<string>(s, StringComparer.OrdinalIgnoreCase) :> IReadOnlySet<_>

    let defaultOptions contextLabel =
        { ContextLabel = contextLabel
          ForbiddenKeywords = defaultForbidden }

    let private ensureNotForbidden (reader: TokenReader) (options: ParseOptions) =
        match reader.TryPeekIdent() with
        | Some peek when options.ForbiddenKeywords.Contains peek ->
            raise (DashSpecParseException($"{options.ContextLabel}: '{peek}' is not valid in a flow block."))
        | _ -> ()

    let parseFlowBlock (reader: TokenReader) (options: ParseOptions) (endKind: string) (endId: string option) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let links = ResizeArray<FlowLinkDef>()

        while not (BlockSyntax.isBlockEnd reader endKind endId) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader endKind endId then
                ()
            else
                ensureNotForbidden reader options

                let saved = reader.SavePosition()
                let fromNode = FlowLinkParser.readEndpointSameLine reader

                if String.IsNullOrWhiteSpace fromNode then
                    raise (reader.Unexpected "link line in flow block")

                let fromPort = FlowLinkParser.tryReadBracketPortSameLine reader

                match FlowLinkParser.tryParseLink reader fromNode fromPort with
                | Some link -> links.Add link
                | None ->
                    reader.RestorePosition saved
                    raise (reader.Unexpected "link line in flow block")

        BlockSyntax.expectBlockEnd reader endKind endId
        links :> IReadOnlyList<_>
