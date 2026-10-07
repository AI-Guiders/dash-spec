using DashSpec.Abstractions.Hosting;

namespace DashSpec.Host.Configuration;

internal sealed class ViewerRuntimeContextAdapter(DashSpecHostContext hostContext) : IViewerRuntimeContext
{
    public string StartupRuntimeConfigPath => hostContext.StartupRuntimeConfigPath;
}
