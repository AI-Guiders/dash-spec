namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

/// Card-local wiring: module inputs → slots (+ bind on slots). Not merged into module dashflow (ADR-0078).
module CardInteriorFlowParser =

    [<CLIMutable>]
    type CardInteriorSlotBind =
        { SlotRef: string
          FilterNames: IReadOnlyList<string> }

    [<CLIMutable>]
    type CardInteriorFlowDefinition =
        { Links: IReadOnlyList<FlowLinkDef>
          SlotBinds: IReadOnlyList<CardInteriorSlotBind> }

    let private parseSlotBind (reader: TokenReader) (cardId: string) =
        let slotRef = reader.ReadIdent()

        if String.IsNullOrWhiteSpace slotRef then
            raise (DashSpecParseException($"Card '{cardId}': bind requires a diagram slot id."))

        let filters =
            if reader.IsOnNewline() then
                reader.SkipNewlines()
                if BlockSyntax.isBlockEnd reader "bind" None then
                    BlockSyntax.expectBlockEnd reader "bind" None
                    Array.empty :> IReadOnlyList<_>
                else
                    PropertyBlockParser.parseCommaListBlock reader "bind" "bind"
            else
                reader.ReadCommaListInline()

        { SlotRef = slotRef
          FilterNames = filters }

    let parseFlowBlock (reader: TokenReader) (cardId: string) =
        BlockSyntax.beginBlock reader
        reader.SkipNewlines()
        let links = ResizeArray<FlowLinkDef>()
        let slotBinds = ResizeArray<CardInteriorSlotBind>()

        while not (BlockSyntax.isBlockEnd reader "flow" None) && not reader.IsEof do
            reader.SkipNewlines()

            if BlockSyntax.isBlockEnd reader "flow" None then
                ()
            elif reader.TryKeyword "bind" then
                slotBinds.Add(parseSlotBind reader cardId)
                reader.SkipNewlines()
            else
                let saved = reader.SavePosition()
                let fromNode = reader.ReadIdent()

                if String.IsNullOrWhiteSpace fromNode then
                    raise (reader.Unexpected "bind, flow link, or end flow")

                let fromPort = FlowLinkParser.tryReadBracketPortSameLine reader

                match FlowLinkParser.tryParseLink reader fromNode fromPort with
                | Some link -> links.Add link
                | None ->
                    reader.RestorePosition saved
                    raise (reader.Unexpected "bind or flow link (producer [port] -> [port] consumer)")

        BlockSyntax.expectBlockEnd reader "flow" None

        { Links = links :> IReadOnlyList<_>
          SlotBinds = slotBinds :> IReadOnlyList<_> }
