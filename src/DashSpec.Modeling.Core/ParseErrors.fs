namespace DashSpec.Modeling.Core

open System

/// <summary>DS001..DS004: Block balance exception (`end` without opener, mismatched keyword/id, or unclosed block).</summary>
type DashSpecBlockBalanceException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)

/// <summary>DS010: Missing or mismatched `module &lt;kind&gt; &lt;id&gt;` header or duplicate `namespace` directive.</summary>
type DashSpecUnitHeaderException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(message: string, ?sourceOffset: int) =
        DashSpecUnitHeaderException(DashSpecRules.descriptor DashSpecRule.InvalidUnitHeader, message, ?sourceOffset = sourceOffset)

/// <summary>DS011: Unexpected statement, clause, or block inside a container.</summary>
and DashSpecUnexpectedStatementException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(message: string, ?sourceOffset: int) =
        DashSpecUnexpectedStatementException(DashSpecRules.descriptor DashSpecRule.UnexpectedStatement, message, ?sourceOffset = sourceOffset)

/// <summary>DS012: Duplicate declaration of a type, field, group, link, filter, source, transformer, or property.</summary>
and DashSpecDuplicateDeclarationException(descriptor: DashSpecRuleDescriptor, identifier: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(identifier: string, message: string, ?sourceOffset: int) =
        DashSpecDuplicateDeclarationException(DashSpecRules.descriptor DashSpecRule.DuplicateDeclaration, identifier, message, ?sourceOffset = sourceOffset)
    member _.Identifier = identifier

/// <summary>DS013: Missing required block, property, or table cell.</summary>
and DashSpecMissingMemberException(descriptor: DashSpecRuleDescriptor, memberName: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(memberName: string, message: string, ?sourceOffset: int) =
        DashSpecMissingMemberException(DashSpecRules.descriptor DashSpecRule.MissingRequiredMember, memberName, message, ?sourceOffset = sourceOffset)
    member _.MemberName = memberName

/// <summary>DS014: Unknown property key or table column outside the construct's schema/vocabulary.</summary>
and DashSpecUnknownPropertyException(descriptor: DashSpecRuleDescriptor, propertyName: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(propertyName: string, message: string, ?sourceOffset: int) =
        DashSpecUnknownPropertyException(DashSpecRules.descriptor DashSpecRule.UnknownPropertyOrColumn, propertyName, message, ?sourceOffset = sourceOffset)
    member _.PropertyName = propertyName

/// <summary>DS015: Invalid value literal or value-type constraint violation.</summary>
and DashSpecInvalidValueException(descriptor: DashSpecRuleDescriptor, message: string, ?propertyName: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(message: string, ?propertyName: string, ?sourceOffset: int) =
        DashSpecInvalidValueException(DashSpecRules.descriptor DashSpecRule.InvalidValueLiteral, message, ?propertyName = propertyName, ?sourceOffset = sourceOffset)
    member _.PropertyName = propertyName

/// <summary>DS016: Malformed pipe table (e.g. header row with no data rows).</summary>
and DashSpecTableStructureException(descriptor: DashSpecRuleDescriptor, tableName: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(tableName: string, message: string, ?sourceOffset: int) =
        DashSpecTableStructureException(DashSpecRules.descriptor DashSpecRule.MalformedTable, tableName, message, ?sourceOffset = sourceOffset)
    member _.TableName = tableName

/// <summary>DS017: Layout scope mismatch or flow link placed in the wrong qualified flow block.</summary>
and DashSpecScopeMismatchException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(message: string, ?sourceOffset: int) =
        DashSpecScopeMismatchException(DashSpecRules.descriptor DashSpecRule.ScopeMismatch, message, ?sourceOffset = sourceOffset)

/// <summary>DS018: Unresolved module import, diagram, type, palette, or flow node reference.</summary>
and DashSpecUnresolvedReferenceException(descriptor: DashSpecRuleDescriptor, target: string, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(target: string, message: string, ?sourceOffset: int) =
        DashSpecUnresolvedReferenceException(DashSpecRules.descriptor DashSpecRule.UnresolvedImportOrReference, target, message, ?sourceOffset = sourceOffset)
    member _.Target = target

/// <summary>DS019: Type mismatch across ports, fields, or composite sources.</summary>
and DashSpecTypeMismatchException(descriptor: DashSpecRuleDescriptor, message: string, ?sourceOffset: int) =
    inherit DashSpecParseException(message, ?sourceOffset = sourceOffset, descriptor = descriptor)
    new(message: string, ?sourceOffset: int) =
        DashSpecTypeMismatchException(DashSpecRules.descriptor DashSpecRule.TypeMismatch, message, ?sourceOffset = sourceOffset)

/// <summary>SSOT catalog of all <see cref="DashSpecRuleDescriptor"/> entries — each rule carries its
/// Code, Severity, Title, and typed Exception constructor in one record.</summary>
and DashSpecRules =

    static let mk rule code severity title ctor =
        let rec d =
            { Rule = rule
              Code = code
              Severity = severity
              Title = title
              CreateException = fun (msg, off) -> ctor d msg off }
        d

    static let descriptors =
        [ mk DashSpecRule.ExtraEndBlock "DS001" DashSpecDiagnosticSeverity.Error "unexpected end block"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.MismatchedEndKeyword "DS002" DashSpecDiagnosticSeverity.Error "end block does not match its opener"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.MismatchedEndIdentifier "DS003" DashSpecDiagnosticSeverity.Error "end block identifier does not match its opener"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.UnclosedBlock "DS004" DashSpecDiagnosticSeverity.Error "block is not closed"
              (fun d msg off -> DashSpecBlockBalanceException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.EmptyOutlineSpan "DS005" DashSpecDiagnosticSeverity.Error "outline node has an empty span"
              (fun d msg off -> DashSpecParseException(msg, ?sourceOffset = off, descriptor = d))
          mk DashSpecRule.CardReferenceOutsideCards "DS006" DashSpecDiagnosticSeverity.Warning "card reference is not nested under a cards block"
              (fun d msg off -> DashSpecScopeMismatchException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.RoundTripOutlineChanged "DS007" DashSpecDiagnosticSeverity.Error "outline changed after serialize round-trip"
              (fun d msg off -> DashSpecParseException(msg, ?sourceOffset = off, descriptor = d))
          mk DashSpecRule.ImportAfterCode "DS008" DashSpecDiagnosticSeverity.Error "import must come before code sections in the module header"
              (fun d msg off -> DashSpecUnitHeaderException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.ParseError "DS009" DashSpecDiagnosticSeverity.Error "syntax error"
              (fun d msg off -> DashSpecParseException(msg, ?sourceOffset = off, descriptor = d))
          mk DashSpecRule.InvalidUnitHeader "DS010" DashSpecDiagnosticSeverity.Error "invalid unit header or duplicate namespace directive"
              (fun d msg off -> DashSpecUnitHeaderException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.UnexpectedStatement "DS011" DashSpecDiagnosticSeverity.Error "unexpected statement or block in container"
              (fun d msg off -> DashSpecUnexpectedStatementException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.DuplicateDeclaration "DS012" DashSpecDiagnosticSeverity.Error "duplicate declaration identifier"
              (fun d msg off -> DashSpecDuplicateDeclarationException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.MissingRequiredMember "DS013" DashSpecDiagnosticSeverity.Error "missing required block, property, or table cell"
              (fun d msg off -> DashSpecMissingMemberException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.UnknownPropertyOrColumn "DS014" DashSpecDiagnosticSeverity.Error "unknown property key or table column"
              (fun d msg off -> DashSpecUnknownPropertyException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.InvalidValueLiteral "DS015" DashSpecDiagnosticSeverity.Error "invalid value literal or value type constraint violation"
              (fun d msg off -> DashSpecInvalidValueException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.MalformedTable "DS016" DashSpecDiagnosticSeverity.Error "table requires at least one data row after header"
              (fun d msg off -> DashSpecTableStructureException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.ScopeMismatch "DS017" DashSpecDiagnosticSeverity.Error "scope or flow-block role mismatch"
              (fun d msg off -> DashSpecScopeMismatchException(d, msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.UnresolvedImportOrReference "DS018" DashSpecDiagnosticSeverity.Error "unresolved import or symbol reference"
              (fun d msg off -> DashSpecUnresolvedReferenceException(d, "", msg, ?sourceOffset = off) :> DashSpecParseException)
          mk DashSpecRule.TypeMismatch "DS019" DashSpecDiagnosticSeverity.Error "type mismatch across ports, fields, or composite sources"
              (fun d msg off -> DashSpecTypeMismatchException(d, msg, ?sourceOffset = off) :> DashSpecParseException) ]

    static let byRule = descriptors |> List.map (fun d -> d.Rule, d) |> Map.ofList
    static let byCode = descriptors |> List.map (fun d -> d.Code, d) |> Map.ofList

    static member allDescriptors = descriptors
    static member all = descriptors |> List.map (fun d -> d.Rule)
    static member descriptor(rule: DashSpecRule) = byRule.[rule]
    static member code(rule: DashSpecRule) = byRule.[rule].Code
    static member severity(rule: DashSpecRule) = byRule.[rule].Severity
    static member title(rule: DashSpecRule) = byRule.[rule].Title
    static member tryOfCode(value: string) = byCode |> Map.tryFind value |> Option.map (fun d -> d.Rule)
    static member createException (rule: DashSpecRule) (message: string) (sourceOffset: int option) =
        byRule.[rule].CreateException(message, sourceOffset)
    static member raiseRule (rule: DashSpecRule) (message: string) (sourceOffset: int option) =
        raise (byRule.[rule].CreateException(message, sourceOffset))

/// <summary>A dashflow contract violation surfaced past the syntax layer —
/// composite source composition, row-type resolution, or card materialization
/// (ADR-0126 §1.1). Distinct from DashSpecParseException: the flow text parsed,
/// the model itself does not compose.</summary>
type DashSpecFlowException(message: string) =
    inherit Exception(message)
