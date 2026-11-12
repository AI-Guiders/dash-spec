namespace DashSpec.Modeling.Core

open System
open System.Collections.Concurrent
open System.IO
open System.Reflection

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
    | UnclosedToken
    | UnexpectedToken
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

/// <summary>Self-contained rule/checker descriptor: owns its Id, Code, Severity, Title, and typed Exception factory.</summary>
and [<CustomEquality; NoComparison>] DashSpecRuleDescriptor =
    { Id: string
      Rule: DashSpecRule
      Code: string
      Severity: DashSpecDiagnosticSeverity
      Title: string
      CreateException: string * int option -> DashSpecParseException }

    override this.Equals(obj) =
        match obj with
        | :? DashSpecRuleDescriptor as other ->
            String.Equals(this.Id, other.Id, StringComparison.OrdinalIgnoreCase)
        | _ -> false

    override this.GetHashCode() = StringComparer.OrdinalIgnoreCase.GetHashCode(this.Id)

/// <summary>Base parse/validation exception owned by a required <see cref="DashSpecRuleDescriptor"/>.</summary>
and DashSpecParseException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit Exception(message)

    new(message: string, ?sourceOffset: int) =
        DashSpecParseException(DashSpecRules.descriptor DashSpecRule.ParseError, message, ?sourceOffset = sourceOffset)

    /// <summary>0-based character offset in source text, when known.</summary>
    member _.SourceOffset = sourceOffset |> Option.toNullable

    /// <summary>Rule descriptor that produced this exception (never None).</summary>
    member _.Descriptor = descriptor

    /// <summary>Language rule violated by this error.</summary>
    member _.Rule = descriptor.Rule

    /// <summary>Stable DS### diagnostic code for this error.</summary>
    member _.Code = descriptor.Code

    /// <summary>Diagnostic severity from the owning rule.</summary>
    member _.Severity = descriptor.Severity

/// <summary>DS001..DS004: Block balance exception (`end` without opener, mismatched keyword/id, or unclosed block).</summary>
and DashSpecBlockBalanceException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)

/// <summary>DS020: Unclosed lexical token (`"…"`, `"""…"""`, `[[…]]`, or `/* … */`).</summary>
and DashSpecUnclosedTokenException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?sourceOffset: int) =
        DashSpecUnclosedTokenException(DashSpecRules.descriptor DashSpecRule.UnclosedToken, message, ?sourceOffset = sourceOffset)

/// <summary>DS021: Unexpected lexical character or malformed token (`-`, `#`, unknown symbol).</summary>
and DashSpecUnexpectedTokenException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?sourceOffset: int) =
        DashSpecUnexpectedTokenException(DashSpecRules.descriptor DashSpecRule.UnexpectedToken, message, ?sourceOffset = sourceOffset)

/// <summary>DS010: Missing or mismatched `module &lt;kind&gt; &lt;id&gt;` header or duplicate `namespace` directive.</summary>
and DashSpecUnitHeaderException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?sourceOffset: int) =
        DashSpecUnitHeaderException(DashSpecRules.descriptor DashSpecRule.InvalidUnitHeader, message, ?sourceOffset = sourceOffset)

/// <summary>DS011: Unexpected statement, clause, or block inside a container.</summary>
and DashSpecUnexpectedStatementException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?sourceOffset: int) =
        DashSpecUnexpectedStatementException(DashSpecRules.descriptor DashSpecRule.UnexpectedStatement, message, ?sourceOffset = sourceOffset)

/// <summary>DS012: Duplicate declaration of a type, field, group, link, filter, source, transformer, or property.</summary>
and DashSpecDuplicateDeclarationException(descriptor: DashSpecRuleDescriptor, identifier: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(identifier: string, message: string, ?sourceOffset: int) =
        DashSpecDuplicateDeclarationException(DashSpecRules.descriptor DashSpecRule.DuplicateDeclaration, identifier, message, ?sourceOffset = sourceOffset)
    member _.Identifier = identifier

/// <summary>DS013: Missing required block, property, or table cell.</summary>
and DashSpecMissingMemberException(descriptor: DashSpecRuleDescriptor, memberName: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(memberName: string, message: string, ?sourceOffset: int) =
        DashSpecMissingMemberException(DashSpecRules.descriptor DashSpecRule.MissingRequiredMember, memberName, message, ?sourceOffset = sourceOffset)
    member _.MemberName = memberName

/// <summary>DS014: Unknown property key or table column outside the construct's schema/vocabulary.</summary>
and DashSpecUnknownPropertyException(descriptor: DashSpecRuleDescriptor, propertyName: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(propertyName: string, message: string, ?sourceOffset: int) =
        DashSpecUnknownPropertyException(DashSpecRules.descriptor DashSpecRule.UnknownPropertyOrColumn, propertyName, message, ?sourceOffset = sourceOffset)
    member _.PropertyName = propertyName

/// <summary>DS015: Invalid value literal or value-type constraint violation.</summary>
and DashSpecInvalidValueException(descriptor: DashSpecRuleDescriptor, message: string, ?propertyName: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?propertyName: string, ?sourceOffset: int) =
        DashSpecInvalidValueException(DashSpecRules.descriptor DashSpecRule.InvalidValueLiteral, message, ?propertyName = propertyName, ?sourceOffset = sourceOffset)
    member _.PropertyName = propertyName

/// <summary>DS016: Malformed pipe table (e.g. header row with no data rows).</summary>
and DashSpecTableStructureException(descriptor: DashSpecRuleDescriptor, tableName: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(tableName: string, message: string, ?sourceOffset: int) =
        DashSpecTableStructureException(DashSpecRules.descriptor DashSpecRule.MalformedTable, tableName, message, ?sourceOffset = sourceOffset)
    member _.TableName = tableName

/// <summary>DS017: Layout scope mismatch or flow link placed in the wrong qualified flow block.</summary>
and DashSpecScopeMismatchException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?sourceOffset: int) =
        DashSpecScopeMismatchException(DashSpecRules.descriptor DashSpecRule.ScopeMismatch, message, ?sourceOffset = sourceOffset)

/// <summary>DS018: Unresolved module import, diagram, type, palette, or flow node reference.</summary>
and DashSpecUnresolvedReferenceException(descriptor: DashSpecRuleDescriptor, target: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(target: string, message: string, ?sourceOffset: int) =
        DashSpecUnresolvedReferenceException(DashSpecRules.descriptor DashSpecRule.UnresolvedImportOrReference, target, message, ?sourceOffset = sourceOffset)
    member _.Target = target

/// <summary>DS019: Type mismatch across ports, fields, or composite sources.</summary>
and DashSpecTypeMismatchException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(descriptor, message, ?sourceOffset = sourceOffset)
    new(message: string, ?sourceOffset: int) =
        DashSpecTypeMismatchException(DashSpecRules.descriptor DashSpecRule.TypeMismatch, message, ?sourceOffset = sourceOffset)

/// <summary>Open registry of rule/checker descriptors + embedded <c>stdlib/rules/rules.ds</c> checker manifest.</summary>
and DashSpecRules =

    static let parseSeverity (raw: string) =
        match raw.Trim().ToLowerInvariant() with
        | "warning" | "warn" -> DashSpecDiagnosticSeverity.Warning
        | "info" | "information" -> DashSpecDiagnosticSeverity.Information
        | _ -> DashSpecDiagnosticSeverity.Error

    static let configuredCheckers : Map<string, DashSpecDiagnosticSeverity> =
        let assembly = Assembly.GetExecutingAssembly()
        use stream = assembly.GetManifestResourceStream("stdlib/rules/rules.ds")
        if isNull stream then
            Map.empty
        else
            use reader = new StreamReader(stream)
            let lines =
                reader.ReadToEnd().Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun l -> l.Trim())
                |> Array.filter (fun l -> l.StartsWith('|'))

            if lines.Length <= 1 then
                Map.empty
            else
                lines.[1..]
                |> Array.choose (fun line ->
                    let cells = line.Split('|', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries)
                    if cells.Length >= 2 && not (cells.[0] |> Seq.forall (fun c -> c = '-')) then
                        Some(cells.[0], parseSeverity cells.[1])
                    else
                        None)
                |> Map.ofArray

    static let mk id rule code defaultSeverity title ctor =
        let severity =
            configuredCheckers
            |> Map.tryFind id
            |> Option.defaultValue defaultSeverity

        let rec d =
            { Id = id
              Rule = rule
              Code = code
              Severity = severity
              Title = title
              CreateException = fun (msg, off) -> ctor d msg off }
        d

    static let builtinDescriptors =
        [ mk "ExtraEndBlock" DashSpecRule.ExtraEndBlock "DS001" DashSpecDiagnosticSeverity.Error "unexpected end block"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "MismatchedEndKeyword" DashSpecRule.MismatchedEndKeyword "DS002" DashSpecDiagnosticSeverity.Error "end block does not match its opener"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "MismatchedEndIdentifier" DashSpecRule.MismatchedEndIdentifier "DS003" DashSpecDiagnosticSeverity.Error "end block identifier does not match its opener"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "UnclosedBlock" DashSpecRule.UnclosedBlock "DS004" DashSpecDiagnosticSeverity.Error "block is not closed"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "EmptyOutlineSpan" DashSpecRule.EmptyOutlineSpan "DS005" DashSpecDiagnosticSeverity.Error "outline node has an empty span"
              (fun d msg off -> DashSpecParseException(d, msg, ?sourceOffset = off))
          mk "CardReferenceOutsideCards" DashSpecRule.CardReferenceOutsideCards "DS006" DashSpecDiagnosticSeverity.Warning "card reference is not nested under a cards block"
              (fun d msg off -> DashSpecScopeMismatchException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "RoundTripOutlineChanged" DashSpecRule.RoundTripOutlineChanged "DS007" DashSpecDiagnosticSeverity.Error "outline changed after serialize round-trip"
              (fun d msg off -> DashSpecParseException(d, msg, ?sourceOffset = off))
          mk "ImportAfterCode" DashSpecRule.ImportAfterCode "DS008" DashSpecDiagnosticSeverity.Error "import must come before code sections in the module header"
              (fun d msg off -> DashSpecUnitHeaderException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "ParseError" DashSpecRule.ParseError "DS009" DashSpecDiagnosticSeverity.Error "syntax error"
              (fun d msg off -> DashSpecParseException(d, msg, ?sourceOffset = off))
          mk "InvalidUnitHeader" DashSpecRule.InvalidUnitHeader "DS010" DashSpecDiagnosticSeverity.Error "invalid unit header or duplicate namespace directive"
              (fun d msg off -> DashSpecUnitHeaderException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "UnexpectedStatement" DashSpecRule.UnexpectedStatement "DS011" DashSpecDiagnosticSeverity.Error "unexpected statement or block in container"
              (fun d msg off -> DashSpecUnexpectedStatementException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "DuplicateDeclaration" DashSpecRule.DuplicateDeclaration "DS012" DashSpecDiagnosticSeverity.Error "duplicate declaration identifier"
              (fun d msg off -> DashSpecDuplicateDeclarationException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "MissingRequiredMember" DashSpecRule.MissingRequiredMember "DS013" DashSpecDiagnosticSeverity.Error "missing required block, property, or table cell"
              (fun d msg off -> DashSpecMissingMemberException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "UnknownPropertyOrColumn" DashSpecRule.UnknownPropertyOrColumn "DS014" DashSpecDiagnosticSeverity.Error "unknown property key or table column"
              (fun d msg off -> DashSpecUnknownPropertyException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "InvalidValueLiteral" DashSpecRule.InvalidValueLiteral "DS015" DashSpecDiagnosticSeverity.Error "invalid value literal or value type constraint violation"
              (fun d msg off -> DashSpecInvalidValueException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "MalformedTable" DashSpecRule.MalformedTable "DS016" DashSpecDiagnosticSeverity.Error "table requires at least one data row after header"
              (fun d msg off -> DashSpecTableStructureException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "ScopeMismatch" DashSpecRule.ScopeMismatch "DS017" DashSpecDiagnosticSeverity.Error "scope or flow-block role mismatch"
              (fun d msg off -> DashSpecScopeMismatchException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "UnresolvedImportOrReference" DashSpecRule.UnresolvedImportOrReference "DS018" DashSpecDiagnosticSeverity.Error "unresolved import or symbol reference"
              (fun d msg off -> DashSpecUnresolvedReferenceException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "TypeMismatch" DashSpecRule.TypeMismatch "DS019" DashSpecDiagnosticSeverity.Error "type mismatch across ports, fields, or composite sources"
              (fun d msg off -> DashSpecTypeMismatchException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "UnclosedToken" DashSpecRule.UnclosedToken "DS020" DashSpecDiagnosticSeverity.Error "unclosed string, raw block, or block comment"
              (fun d msg off -> DashSpecUnclosedTokenException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk "UnexpectedToken" DashSpecRule.UnexpectedToken "DS021" DashSpecDiagnosticSeverity.Error "unexpected character or malformed lexical token"
              (fun d msg off -> DashSpecUnexpectedTokenException(d, msg, ?sourceOffset = off) :> DashSpecParseException) ]

    static let byRule = builtinDescriptors |> List.map (fun d -> d.Rule, d) |> Map.ofList
    static let byCode = ConcurrentDictionary<string, DashSpecRuleDescriptor>(StringComparer.OrdinalIgnoreCase)
    static let byId = ConcurrentDictionary<string, DashSpecRuleDescriptor>(StringComparer.OrdinalIgnoreCase)

    static do
        for d in builtinDescriptors do
            byCode.[d.Code] <- d
            byId.[d.Id] <- d

    static member register(descriptor: DashSpecRuleDescriptor) =
        byCode.[descriptor.Code] <- descriptor
        byId.[descriptor.Id] <- descriptor

    static member isCheckerEnabled(checkerId: string) =
        configuredCheckers.IsEmpty || configuredCheckers.ContainsKey checkerId

    static member configuredSeverity(checkerId: string, fallback: DashSpecDiagnosticSeverity) =
        configuredCheckers |> Map.tryFind checkerId |> Option.defaultValue fallback

    static member allDescriptors = builtinDescriptors
    static member all = builtinDescriptors |> List.map (fun d -> d.Rule)
    static member descriptor(rule: DashSpecRule) = byRule.[rule]
    static member tryById(id: string) =
        match byId.TryGetValue id with
        | true, d -> Some d
        | false, _ -> None
    static member code(rule: DashSpecRule) = byRule.[rule].Code
    static member severity(rule: DashSpecRule) = byRule.[rule].Severity
    static member title(rule: DashSpecRule) = byRule.[rule].Title
    static member tryOfCode(value: string) =
        match byCode.TryGetValue value with
        | true, d -> Some d.Rule
        | false, _ -> None
    static member createException (rule: DashSpecRule) (message: string) (sourceOffset: int option) =
        byRule.[rule].CreateException(message, sourceOffset)
    static member raiseRule (rule: DashSpecRule) (message: string) (sourceOffset: int option) =
        raise (byRule.[rule].CreateException(message, sourceOffset))
