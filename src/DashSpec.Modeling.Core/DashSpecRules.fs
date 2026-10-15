namespace DashSpec.Modeling.Core

/// <summary>Language rules. Each rule has one stable code (DS###), one severity and one title; a code never changes meaning.</summary>
[<RequireQualifiedAccess>]
type DashSpecRule =
    | ExtraEndBlock
    | MismatchedEndKeyword
    | MismatchedEndIdentifier
    | UnclosedBlock
    | EmptyOutlineSpan
    | CardReferenceOutsideCards
    | RoundTripOutlineChanged
    | ImportAfterCode
    | ParseError

module DashSpecRules =

    let all =
        [ DashSpecRule.ExtraEndBlock
          DashSpecRule.MismatchedEndKeyword
          DashSpecRule.MismatchedEndIdentifier
          DashSpecRule.UnclosedBlock
          DashSpecRule.EmptyOutlineSpan
          DashSpecRule.CardReferenceOutsideCards
          DashSpecRule.RoundTripOutlineChanged
          DashSpecRule.ImportAfterCode
          DashSpecRule.ParseError ]

    let code (rule: DashSpecRule) : string =
        match rule with
        | DashSpecRule.ExtraEndBlock -> "DS001"
        | DashSpecRule.MismatchedEndKeyword -> "DS002"
        | DashSpecRule.MismatchedEndIdentifier -> "DS003"
        | DashSpecRule.UnclosedBlock -> "DS004"
        | DashSpecRule.EmptyOutlineSpan -> "DS005"
        | DashSpecRule.CardReferenceOutsideCards -> "DS006"
        | DashSpecRule.RoundTripOutlineChanged -> "DS007"
        | DashSpecRule.ImportAfterCode -> "DS008"
        | DashSpecRule.ParseError -> "DS009"

    let tryOfCode (value: string) : DashSpecRule option =
        all |> List.tryFind (fun rule -> code rule = value)

    let severity (rule: DashSpecRule) : DashSpecDiagnosticSeverity =
        match rule with
        | DashSpecRule.CardReferenceOutsideCards -> DashSpecDiagnosticSeverity.Warning
        | _ -> DashSpecDiagnosticSeverity.Error

    let title (rule: DashSpecRule) : string =
        match rule with
        | DashSpecRule.ExtraEndBlock -> "unexpected end block"
        | DashSpecRule.MismatchedEndKeyword -> "end block does not match its opener"
        | DashSpecRule.MismatchedEndIdentifier -> "end block identifier does not match its opener"
        | DashSpecRule.UnclosedBlock -> "block is not closed"
        | DashSpecRule.EmptyOutlineSpan -> "outline node has an empty span"
        | DashSpecRule.CardReferenceOutsideCards -> "card reference is not nested under a cards block"
        | DashSpecRule.RoundTripOutlineChanged -> "outline changed after serialize round-trip"
        | DashSpecRule.ImportAfterCode -> "import must come before code sections in the module header"
        | DashSpecRule.ParseError -> "syntax error"

    let isBlockBalance (rule: DashSpecRule) : bool =
        match rule with
        | DashSpecRule.ExtraEndBlock
        | DashSpecRule.MismatchedEndKeyword
        | DashSpecRule.MismatchedEndIdentifier
        | DashSpecRule.UnclosedBlock -> true
        | _ -> false
