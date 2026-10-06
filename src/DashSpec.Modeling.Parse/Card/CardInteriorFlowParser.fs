namespace DashSpec.Modeling.Parse.Card

open System
open System.Collections.Generic
open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

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
                raise (DashSpecParseException($"Card '{cardId}': 'bind' is not valid in a flow block."))
            elif reader.TryKeyword "slot" then
                raise (DashSpecParseException($"Card '{cardId}': 'slot' is not valid in a flow block."))
            else
                let saved = reader.SavePosition()
                let fromNode = reader.ReadIdent()

                if String.IsNullOrWhiteSpace fromNode then
                    raise (reader.Unexpected "link line in flow block")

                let fromPort = FlowLinkParser.tryReadBracketPortSameLine reader

                match FlowLinkParser.tryParseLink reader fromNode fromPort with
                | Some link -> links.Add link
                | None ->
                    reader.RestorePosition saved
                    raise (reader.Unexpected "link line in flow block")

        BlockSyntax.expectBlockEnd reader "flow" None

        { Links = links :> IReadOnlyList<_> }
