namespace DashSpec.Modeling.Parse.Card

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

module CardInteriorFlowParser =

    let createBuilder () = FlowGraphSections.Builder()

    let tryAddQualifiedBlock (reader: TokenReader) (builder: FlowGraphSections.Builder) (cardId: string) =
        QualifiedFlowBlockParser.tryParseAndAdd
            reader
            builder
            FlowGraphKindRegistry.FlowGraphScope.CardInterior
            $"Card '{cardId}'"

    let rejectLegacyFlow (reader: TokenReader) (cardId: string) =
        QualifiedFlowBlockParser.tryRejectLegacyFlow reader $"Card '{cardId}'"

    let toDefinition (builder: FlowGraphSections.Builder) : CardInteriorFlowDefinition option =
        builder.ToSections() |> Option.map (fun sections -> { Sections = sections })
