using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viewer;
using DashSpec.Viewer.Plugins;

namespace DashSpec.Viewer;

public sealed class ViewerPluginHost(DashSpecContributorRegistry contributors, DashSpecPluginCapabilities capabilities)
    : IViewerPluginHost
{
    public IDashSpecContributorRegistry Contributors => contributors;

    public DashSpecPluginCapabilities Capabilities => capabilities;
}
