using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace DashSpec.Host.E2E.Hosting;

/// <summary>
/// Boots Host with committed <c>dash-spec.toml</c> only (skips gitignored <c>dash-spec.local.toml</c>).
/// Planet SSOT: <c>samples/demo/demo.dashhost</c> → default catalog entry <c>demo_soak</c>.
/// </summary>
public sealed class DashSpecWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Real HTTP endpoint for Playwright (TestServer is in-memory only).</summary>
    /// <remarks>
    /// The port is picked per run. A fixed port stayed bound whenever a previous test host did not exit,
    /// and every following run then failed with "address already in use".
    /// </remarks>
    public string ListenUrl { get; } = $"http://127.0.0.1:{ResolveFreePort()}";

    public string BaseUrl => ListenUrl.TrimEnd('/');

    private IHost? _kestrelHost;

    /// <summary>Asks the OS for an unused loopback port.</summary>
    private static int ResolveFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(ResolveHostProjectDirectory());
        Environment.SetEnvironmentVariable("DASHSPEC_SKIP_LOCAL_CONFIG", "1");
        builder.UseEnvironment("E2E");
        builder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseKestrel();
            webHostBuilder.UseUrls(ListenUrl);
        });

        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kestrelHost?.StopAsync().GetAwaiter().GetResult();
            _kestrelHost?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string ResolveHostProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "DashSpec.Host");
            if (File.Exists(Path.Combine(candidate, "dash-spec.toml")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate DashSpec.Host project (dash-spec.toml).");
    }
}
