using DashSpec.Core.Parsing;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Viewer.Plugins;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportParseOptionsSource(DashSpecParseOptionsProvider inner) : IReportParseOptionsSource
{
    public DashSpecParseOptions CreateOptions() => inner.CreateOptions();
}
