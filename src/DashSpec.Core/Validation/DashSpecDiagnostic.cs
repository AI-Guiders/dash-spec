namespace DashSpec.Core.Validation;

public enum DashSpecDiagnosticSeverity
{
    Error,
    Warning,
    Information,
}

public sealed record DashSpecDiagnostic(
    int Line,
    int Character,
    int EndLine,
    int EndCharacter,
    string Message,
    DashSpecDiagnosticSeverity Severity = DashSpecDiagnosticSeverity.Error);
