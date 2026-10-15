namespace DashSpec.CodeCenter

open DashSpec.Modeling.Core
open DashSpec.Modeling.Parse.Syntax

/// <summary>CodeCenter view of language rules: codes and severities come from DashSpec.Modeling.Core.</summary>
module DashSpecDiagnosticCatalog =

    let private severityText (rule: DashSpecRule) =
        match DashSpecRules.severity rule with
        | DashSpecDiagnosticSeverity.Warning -> "warning"
        | _ -> "error"

    let ofRule (rule: DashSpecRule) (span: TextSpan) (message: string) : ProfileLawDiagnostic =
        { Code = DashSpecRules.code rule
          Message = message
          Start = span.Start
          Length = max 1 span.Length
          Severity = severityText rule }

    let ofFinding (finding: DashSpecRuleFinding) : ProfileLawDiagnostic =
        ofRule finding.Rule finding.Span finding.Message
