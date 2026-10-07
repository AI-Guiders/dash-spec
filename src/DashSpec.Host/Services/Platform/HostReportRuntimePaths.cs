using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportRuntimePaths(IHostPathResolver paths) : IReportRuntimePaths
{
    public string ResolveRuntimeConfigPath(string specFullPath, string specText, string defaultSpecDirectory) =>
        paths.ResolveRuntimeConfigPath(specFullPath, specText, defaultSpecDirectory);
}
