namespace DashSpec.Modeling.Core.Tests

open DashSpec.Modeling.Core

/// <summary>Test-only bridge to transitional C# diagnostics (ADR-0048 §7).</summary>
module DiagnosticInterop =
    type LegacyDiagnostic = DashSpec.Core.Validation.DashSpecDiagnostic
    type LegacySeverity = DashSpec.Core.Validation.DashSpecDiagnosticSeverity

    let private toLegacySeverity =
        function
        | DashSpecDiagnosticSeverity.Error -> LegacySeverity.Error
        | DashSpecDiagnosticSeverity.Warning -> LegacySeverity.Warning
        | DashSpecDiagnosticSeverity.Information -> LegacySeverity.Information

    let private fromLegacySeverity (severity: LegacySeverity) =
        match severity with
        | LegacySeverity.Error -> DashSpecDiagnosticSeverity.Error
        | LegacySeverity.Warning -> DashSpecDiagnosticSeverity.Warning
        | LegacySeverity.Information -> DashSpecDiagnosticSeverity.Information
        | _ -> DashSpecDiagnosticSeverity.Error

    let toLegacy (diagnostic: DashSpecDiagnostic) : LegacyDiagnostic =
        LegacyDiagnostic(
            diagnostic.Span.Line,
            diagnostic.Span.Character,
            diagnostic.Span.EndLine,
            diagnostic.Span.EndCharacter,
            diagnostic.Message,
            toLegacySeverity diagnostic.Severity)

    let fromLegacy (diagnostic: LegacyDiagnostic) : DashSpecDiagnostic =
        { Span =
            { Line = diagnostic.Line
              Character = diagnostic.Character
              EndLine = diagnostic.EndLine
              EndCharacter = diagnostic.EndCharacter }
          Message = diagnostic.Message
          Severity = fromLegacySeverity diagnostic.Severity }
