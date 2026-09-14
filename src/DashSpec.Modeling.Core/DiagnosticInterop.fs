namespace DashSpec.Modeling.Core

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

    /// <summary>Project F# diagnostic to transitional C# record.</summary>
    let toLegacy (diagnostic: DashSpecDiagnostic) : LegacyDiagnostic =
        LegacyDiagnostic(
            diagnostic.Span.Line,
            diagnostic.Span.Character,
            diagnostic.Span.EndLine,
            diagnostic.Span.EndCharacter,
            diagnostic.Message,
            toLegacySeverity diagnostic.Severity)

    /// <summary>Import C# diagnostic into F# Modeling SSOT.</summary>
    let fromLegacy (diagnostic: LegacyDiagnostic) : DashSpecDiagnostic =
        { Span =
            { Line = diagnostic.Line
              Character = diagnostic.Character
              EndLine = diagnostic.EndLine
              EndCharacter = diagnostic.EndCharacter }
          Message = diagnostic.Message
          Severity = fromLegacySeverity diagnostic.Severity }
