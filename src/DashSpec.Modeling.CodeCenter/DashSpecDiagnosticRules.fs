namespace DashSpec.Modeling.CodeCenter

open System
open DashSpec.Modeling.Parse.Syntax

/// Stable rule identity; registry order defines auto-generated DS### codes.
[<RequireQualifiedAccess>]
type DashSpecRuleKind =
    | ExtraEndBlock
    | MismatchedEndKeyword
    | MismatchedEndIdentifier
    | UnclosedBlock
    | EmptyOutlineSpan
    | CardReferenceOutsideCards
    | RoundTripOutlineChanged

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

/// Ordered rule catalog — append only; DS codes derive from list position.
module DashSpecRuleRegistry =

    let private orderedKinds =
        [ DashSpecRuleKind.ExtraEndBlock
          DashSpecRuleKind.MismatchedEndKeyword
          DashSpecRuleKind.MismatchedEndIdentifier
          DashSpecRuleKind.UnclosedBlock
          DashSpecRuleKind.EmptyOutlineSpan
          DashSpecRuleKind.CardReferenceOutsideCards
          DashSpecRuleKind.RoundTripOutlineChanged ]

    let allKinds = orderedKinds

    let code (kind: DashSpecRuleKind) =
        let index = orderedKinds |> List.findIndex ((=) kind)
        sprintf "DS%03d" (index + 1)

    let tryKindOfCode (ruleCode: string) =
        orderedKinds
        |> List.tryFind (fun kind -> code kind = ruleCode)

    let kindOf (violation: DashSpecRuleViolation) =
        match violation with
        | DashSpecRuleViolation.ExtraEndBlock _ -> DashSpecRuleKind.ExtraEndBlock
        | DashSpecRuleViolation.MismatchedEndKeyword _ -> DashSpecRuleKind.MismatchedEndKeyword
        | DashSpecRuleViolation.MismatchedEndIdentifier _ -> DashSpecRuleKind.MismatchedEndIdentifier
        | DashSpecRuleViolation.UnclosedBlock _ -> DashSpecRuleKind.UnclosedBlock
        | DashSpecRuleViolation.EmptyOutlineSpan _ -> DashSpecRuleKind.EmptyOutlineSpan
        | DashSpecRuleViolation.CardReferenceOutsideCards _ -> DashSpecRuleKind.CardReferenceOutsideCards

    let severity (kind: DashSpecRuleKind) =
        match kind with
        | DashSpecRuleKind.CardReferenceOutsideCards -> "warning"
        | _ -> "error"

    let span (violation: DashSpecRuleViolation) =
        match violation with
        | DashSpecRuleViolation.ExtraEndBlock span -> span
        | DashSpecRuleViolation.MismatchedEndKeyword(_, _, span) -> span
        | DashSpecRuleViolation.MismatchedEndIdentifier(_, _, span) -> span
        | DashSpecRuleViolation.UnclosedBlock(_, span) -> span
        | DashSpecRuleViolation.EmptyOutlineSpan(_, span) -> span
        | DashSpecRuleViolation.CardReferenceOutsideCards(_, span) -> span

    let message (violation: DashSpecRuleViolation) =
        match violation with
        | DashSpecRuleViolation.ExtraEndBlock _ -> "unexpected end block"
        | DashSpecRuleViolation.MismatchedEndKeyword(expected, actual, _) ->
            $"end {DashSpecBlockKeyword.toEndName actual} does not match opener {DashSpecBlockKeyword.toEndName expected}"
        | DashSpecRuleViolation.MismatchedEndIdentifier(openerId, endId, _) ->
            $"end block identifier '{endId}' does not match opener '{openerId}'"
        | DashSpecRuleViolation.UnclosedBlock(keyword, _) ->
            $"unclosed block '{DashSpecBlockKeyword.toEndName keyword}'"
        | DashSpecRuleViolation.EmptyOutlineSpan(label, _) -> $"outline node '{label}' has empty span"
        | DashSpecRuleViolation.CardReferenceOutsideCards(cardId, _) ->
            $"card reference '{cardId}' is not nested under a cards block"

    let messageFor (kind: DashSpecRuleKind) =
        match kind with
        | DashSpecRuleKind.RoundTripOutlineChanged ->
            "outline signature changed after preserve serialize round-trip"
        | kind ->
            failwith $"rule '{kind}' requires violation payload for message"

    let toDiagnostic (violation: DashSpecRuleViolation) =
        let kind = kindOf violation
        let span = span violation

        { Code = code kind
          Message = message violation
          Start = span.Start
          Length = max 1 span.Length
          Severity = severity kind }

    let diagnostic (kind: DashSpecRuleKind) (span: TextSpan) =
        { Code = code kind
          Message = messageFor kind
          Start = span.Start
          Length = max 1 span.Length
          Severity = severity kind }

    let blockBalanceKinds =
        Set.ofList
            [ DashSpecRuleKind.ExtraEndBlock
              DashSpecRuleKind.MismatchedEndKeyword
              DashSpecRuleKind.MismatchedEndIdentifier
              DashSpecRuleKind.UnclosedBlock ]

    let isBlockBalance (violation: DashSpecRuleViolation) =
        blockBalanceKinds.Contains(kindOf violation)

/// Maps rule violations to host diagnostics (catalog layer).
module DashSpecDiagnosticCatalog =

    let toDiagnostic = DashSpecRuleRegistry.toDiagnostic
