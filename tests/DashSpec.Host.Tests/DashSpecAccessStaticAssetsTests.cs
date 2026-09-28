using DashSpec.Host.Configuration;
using DashSpec.Host.Middleware;
using DashSpec.Host.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class DashSpecAccessStaticAssetsTests
{
    [Theory]
    [InlineData("/lib/chartjs/chart.umd.min.js")]
    [InlineData("/_content/DashSpec.Plugin.Viz.Builtins/js/charts.js")]
    [InlineData("/js/dashspec-download.js")]
    [InlineData("/_framework/blazor.web.js")]
    public async Task Static_shell_assets_are_anonymous_when_api_key_required(string path)
    {
        var options = new DashSpecAccessOptions { ApiKey = "test-key" };
        var validator = new DashSpecAccessValidator(options);
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;

        var invoked = false;
        var middleware = new DashSpecAccessMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }, validator);

        await middleware.InvokeAsync(context);

        Assert.True(invoked, $"Expected anonymous access to {path}");
    }
}
