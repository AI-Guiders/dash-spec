using DashSpec.Execution.Runtime.Platform;
using DashSpec.Viewer.Plugins;

using DashSpec.Execution.Runtime;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportParseOptionsSource(DashSpecParseOptionsProvider inner) : IReportParseOptionsSource
{
    public DashSpecParseOptions CreateOptions() => inner.CreateOptions();
}
