namespace DashSpec.Execution.Compilation.Dialects;

/// <summary>Unknown engines: same fragments as T-SQL per ADR-0006.</summary>
public sealed class GenericDialectBackend : TSqlDialectBackend
{
    public override string Id => "generic";
}
