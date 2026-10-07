using DashSpec.Abstractions.Hosting;

namespace DashSpec.Host.Configuration;

internal sealed class ViewerRuntimeContextAdapter(
    DashSpecHostContext hostContext,
    DashSpecTomlRoot bootstrap) : IViewerRuntimeContext
{
    public string StartupRuntimeConfigPath => hostContext.StartupRuntimeConfigPath;

    public IReadOnlyDictionary<string, string> ReportTimeSettings =>
        bootstrap.ReportTime.ToSettingsDictionary();
}
