using System.Net;
using DashSpec.Host.E2E.Hosting;
using Xunit;

namespace DashSpec.Host.E2E;

/// <summary>ADR-0099 B3.6: Host boots and serves viewer shell without Playwright.</summary>
[Collection(nameof(E2eTestCollection))]
public sealed class ViewerHostSmokeTests
{
    private readonly DashSpecWebApplicationFactory _factory;

    public ViewerHostSmokeTests(DashSpecWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Root_returns_dashspec_html_shell()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("DashSpec", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blazor.web.js", html, StringComparison.OrdinalIgnoreCase);
    }
}
