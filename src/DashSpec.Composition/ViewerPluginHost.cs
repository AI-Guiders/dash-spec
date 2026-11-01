using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viewer;
using DashSpec.Composition.Plugins;

namespace DashSpec.Composition;

public sealed class ViewerPluginHost(DashSpecContributorRegistry contributors, DashSpecPluginCapabilities capabilities)
    : IViewerPluginHost
{
    public IDashSpecContributorRegistry Contributors => contributors;

    public DashSpecPluginCapabilities Capabilities => capabilities;
}
