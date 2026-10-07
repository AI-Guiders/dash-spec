using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportBootstrapEnvironment(DashSpecHostContext hostContext) : IReportBootstrapEnvironment
{
    public string DefaultSpecDirectory => hostContext.DefaultSpecDirectory;
}
