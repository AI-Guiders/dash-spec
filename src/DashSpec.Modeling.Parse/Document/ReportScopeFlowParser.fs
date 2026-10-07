namespace DashSpec.Modeling.Parse.Document

open DashSpec.Modeling.Parse.DataFlow
open DashSpec.Modeling.Parse.Lexing

/// <summary>Parse <c>flow … end flow</c> at report or page scope (ADR-0092).</summary>
module ReportScopeFlowParser =

    let parseReportFlowBlock (reader: TokenReader) =
        let options = FlowLinkBlockParser.defaultOptions "Report"
        let links = FlowLinkBlockParser.parseFlowBlock reader options
        { Links = links }

    let parsePageFlowBlock (reader: TokenReader) (pageId: string) =
        let options = FlowLinkBlockParser.defaultOptions $"Page '{pageId}'"
        let links = FlowLinkBlockParser.parseFlowBlock reader options
        { Links = links }
