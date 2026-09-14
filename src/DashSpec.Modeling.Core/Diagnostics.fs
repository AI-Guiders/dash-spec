namespace DashSpec.Modeling.Core

/// <summary>Declare-time diagnostic severity (F# SSOT per ADR-0048).</summary>
type DashSpecDiagnosticSeverity =
    | Error
    | Warning
    | Information

/// <summary>Zero-based source span for dashspec fragments.</summary>
[<CLIMutable>]
type SourceSpan =
    { Line: int
      Character: int
      EndLine: int
      EndCharacter: int }

/// <summary>Accumulated parse/validation diagnostic.</summary>
[<CLIMutable>]
type DashSpecDiagnostic =
    { Span: SourceSpan
      Message: string
      Severity: DashSpecDiagnosticSeverity }

module DashSpecDiagnostic =
    let create line character endLine endCharacter message severity =
        { Span =
            { Line = line
              Character = character
              EndLine = endLine
              EndCharacter = endCharacter }
          Message = message
          Severity = severity }

    let error line character endLine endCharacter message =
        create line character endLine endCharacter message DashSpecDiagnosticSeverity.Error
