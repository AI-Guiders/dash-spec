using DashSpec.Abstractions.Plugins;

namespace DashSpec.Abstractions.Viewer;

/// <summary>Viewer-facing plugin surface (ADR-0099 B3.2). Host registers concrete registries; Surface consumes this port.</summary>
public interface IViewerPluginHost
{
    IDashSpecContributorRegistry Contributors { get; }

    DashSpecPluginCapabilities Capabilities { get; }
}
