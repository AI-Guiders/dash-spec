using DashSpec.Core.Parsing;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Plugins;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportParseOptionsSource(DashSpecParseOptionsProvider inner) : IReportParseOptionsSource
{
    public DashSpecParseOptions CreateOptions() => inner.CreateOptions();
}
