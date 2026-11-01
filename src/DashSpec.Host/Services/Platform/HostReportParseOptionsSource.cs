using DashSpec.Execution.Runtime.Platform;
using DashSpec.Composition.Plugins;

using DashSpec.Execution.Runtime;

using DashSpec.Compiler.Contract;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportParseOptionsSource(DashSpecParseOptionsProvider inner) : IReportParseOptionsSource
{
    public DashSpecParseOptions CreateOptions() => inner.CreateOptions();
}
