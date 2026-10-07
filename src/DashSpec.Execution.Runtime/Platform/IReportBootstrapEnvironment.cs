namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Host-provided paths for spec bootstrap (ADR-0099).</summary>
public interface IReportBootstrapEnvironment
{
    string DefaultSpecDirectory { get; }
}
