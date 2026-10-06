namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

/// Card-local wiring (flow + route links). See ADR-0091.
module CardInteriorFlowParser =

    [<CLIMutable>]
    type CardInteriorFlowDefinition = { Links: IReadOnlyList<FlowLinkDef> }

    let parseFlowBlock (reader: TokenReader) (cardId: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let links = ResizeArray<FlowLinkDef>()

        while not (BlockSyntax.isBlockEnd reader "flow" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "flow" None then
                ()
            elif reader.TryKeyword "bind" then
                raise (
                    DashSpecParseException(
                        $"Card '{cardId}': 'bind' inside flow was removed; use route links: filterId -> [filterId] slotRef (see ADR-0091)."
                    )
                )
            elif reader.TryKeyword "slot" then
                raise (
                    DashSpecParseException(
                        $"Card '{cardId}': 'slot' inside flow was removed; use module link node [port] -> [rows] slotRef."
                    )
                )
            else
                let saved = reader.SavePosition()
                let fromNode = reader.ReadIdent()

                if String.IsNullOrWhiteSpace fromNode then
                    raise (reader.Unexpected "flow link (producer [port] -> [port] consumer)")

                let fromPort = FlowLinkParser.tryReadBracketPortSameLine reader

                match FlowLinkParser.tryParseLink reader fromNode fromPort with
                | Some link -> links.Add link
                | None ->
                    reader.RestorePosition saved
                    raise (
                        reader.Unexpected
                            "flow link (module node [port] -> [rows] slot, or filter -> [filter] slot)"
                    )

        BlockSyntax.expectBlockEnd reader "flow" None

        { Links = links :> IReadOnlyList<_> }
