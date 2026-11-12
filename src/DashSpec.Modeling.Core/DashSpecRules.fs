namespace DashSpec.Modeling.Core

open System

/// <summary>Language rule identifier (SSOT).</summary>
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
    | InvalidUnitHeader
    | UnexpectedStatement
    | DuplicateDeclaration
    | MissingRequiredMember
    | UnknownPropertyOrColumn
    | InvalidValueLiteral
    | MalformedTable
    | ScopeMismatch
    | UnresolvedImportOrReference
    | TypeMismatch

/// <summary>Self-contained rule descriptor: owns Code, Severity, Title, and its typed Exception factory.</summary>
and [<CustomEquality; NoComparison>] DashSpecRuleDescriptor =
    { Rule: DashSpecRule
      Code: string
      Severity: DashSpecDiagnosticSeverity
      Title: string
      CreateException: string * int option -> DashSpecParseException }

    override this.Equals(obj) =
        match obj with
        | :? DashSpecRuleDescriptor as other -> this.Rule = other.Rule
        | _ -> false

    override this.GetHashCode() = hash this.Rule

/// <summary>Base parse/validation exception owned by a <see cref="DashSpecRuleDescriptor"/>.</summary>
and DashSpecParseException(message: string, ?sourceOffset: int, ?descriptor: DashSpecRuleDescriptor) =
    inherit Exception(message)

    /// <summary>0-based character offset in source text, when known.</summary>
    member _.SourceOffset = sourceOffset |> Option.toNullable

    /// <summary>Rule descriptor that produced this exception.</summary>
    member _.Descriptor = descriptor

    /// <summary>Language rule violated by this error.</summary>
    member _.Rule =
        match descriptor with
        | Some d -> d.Rule
        | None -> DashSpecRule.ParseError

    /// <summary>Stable DS### diagnostic code for this error.</summary>
    member _.Code =
        match descriptor with
        | Some d -> d.Code
        | None -> "DS009"

    /// <summary>Diagnostic severity from the owning rule.</summary>
    member _.Severity =
        match descriptor with
        | Some d -> d.Severity
        | None -> DashSpecDiagnosticSeverity.Error
