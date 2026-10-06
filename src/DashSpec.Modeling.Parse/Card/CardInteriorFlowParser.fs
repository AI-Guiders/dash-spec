namespace DashSpec.Modeling.Parse.Card

open System.Collections.Generic
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

module CardInteriorFlowParser =

    [<CLIMutable>]
    type CardInteriorFlowDefinition = { Links: IReadOnlyList<FlowLinkDef> }

    let parseFlowBlock (reader: TokenReader) (cardId: string) =
        let options =
            FlowLinkBlockParser.defaultOptions $"Card '{cardId}'"

        let links = FlowLinkBlockParser.parseFlowBlock reader options
        { Links = links }
