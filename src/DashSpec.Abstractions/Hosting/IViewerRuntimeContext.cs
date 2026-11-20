namespace DashSpec.Abstractions.Hosting;

/// <summary>Startup runtime paths fixed at host boot (default catalog entry).</summary>
public interface IViewerRuntimeContext
{
    string StartupRuntimeConfigPath { get; }

    string CatalogFullPath => string.Empty;

    string DefaultSpecDirectory => string.Empty;

    IReadOnlyDictionary<string, string> ReportTimeSettings { get; }
}
