namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

/// <summary>Parse qualified <c>{kind} flow</c> at report or page scope (ADR-0093).</summary>
module ReportScopeFlowParser =

    let createBuilder () = FlowGraphSections.Builder()

    let tryAddQualifiedBlock
        (reader: TokenReader)
        (builder: FlowGraphSections.Builder)
        (contextLabel: string)
        =
        QualifiedFlowBlockParser.tryParseAndAdd
            reader
            builder
            FlowGraphKindRegistry.FlowGraphScope.ReportOrPage
            contextLabel

    let rejectLegacyFlow (reader: TokenReader) (contextLabel: string) =
        QualifiedFlowBlockParser.tryRejectLegacyFlow reader contextLabel

    let toDefinition (builder: FlowGraphSections.Builder) : ScopeFlowDefinition option =
        builder.ToSections() |> Option.map (fun sections -> { Sections = sections })
