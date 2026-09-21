namespace DashSpec.Modeling.CodeCenter

open System
open DashSpec.Modeling.Parse.Syntax

/// Typed rule signals before message rendering (ADR-0067 InvariantLaws / conformance).
[<RequireQualifiedAccess>]
type DashSpecRuleViolation =
    | ExtraEndBlock of span: TextSpan
    | MismatchedEndKeyword of
        expected: DashSpecBlockKeyword *
        actual: DashSpecBlockKeyword *
        span: TextSpan
    | MismatchedEndIdentifier of openerId: string * endId: string * span: TextSpan
    | UnclosedBlock of keyword: DashSpecBlockKeyword * span: TextSpan
    | EmptyOutlineSpan of label: string * span: TextSpan
    | CardReferenceOutsideCards of cardId: string * span: TextSpan

/// Maps rule violations to host diagnostics (catalog layer).
module DashSpecDiagnosticCatalog =

    let private diagnostic code message (span: TextSpan) severity =
        { Code = code
          Message = message
          Start = span.Start
          Length = max 1 span.Length
          Severity = severity }

    let toDiagnostic (violation: DashSpecRuleViolation) =
        match violation with
        | DashSpecRuleViolation.ExtraEndBlock span ->
            diagnostic "DS002" "unexpected end block" span "error"

        | DashSpecRuleViolation.MismatchedEndKeyword(expected, actual, span) ->
            diagnostic
                "DS003"
                $"end {DashSpecBlockKeyword.toEndName actual} does not match opener {DashSpecBlockKeyword.toEndName expected}"
                span
                "error"

        | DashSpecRuleViolation.MismatchedEndIdentifier(openerId, endId, span) ->
            diagnostic
                "DS004"
                $"end block identifier '{endId}' does not match opener '{openerId}'"
                span
                "error"

        | DashSpecRuleViolation.UnclosedBlock(keyword, span) ->
            diagnostic
                "DS005"
                $"unclosed block '{DashSpecBlockKeyword.toEndName keyword}'"
                span
                "error"

        | DashSpecRuleViolation.EmptyOutlineSpan(label, span) ->
            diagnostic "DS006" $"outline node '{label}' has empty span" span "error"

        | DashSpecRuleViolation.CardReferenceOutsideCards(cardId, span) ->
            diagnostic
                "DS007"
                $"card reference '{cardId}' is not nested under a cards block"
                span
                "warning"
