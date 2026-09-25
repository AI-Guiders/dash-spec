using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DashSpec.Host.Tests;

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "DashSpec.Host.Tests";

    public string ContentRootPath { get; set; } = string.Empty;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
