using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace DashSpec.Host.E2E.Hosting;

public sealed class DashSpecWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Real HTTP endpoint for Playwright (TestServer is in-memory only).</summary>
    public const string ListenUrl = "http://127.0.0.1:5199";

    public string BaseUrl { get; } = ListenUrl.TrimEnd('/');

    private IHost? _kestrelHost;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(ResolveHostProjectDirectory());
        builder.UseEnvironment(Environments.Development);
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
