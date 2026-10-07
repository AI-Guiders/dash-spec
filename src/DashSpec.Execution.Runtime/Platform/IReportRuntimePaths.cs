namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Resolve runtime manifest paths for spec bootstrap (host provides impl).</summary>
public interface IReportRuntimePaths
{
    string ResolveRuntimeConfigPath(string specFullPath, string specText, string defaultSpecDirectory);
}
