using DashSpec.Abstractions.Plugins;
using DashSpec.Host.Services.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class DiagnosticsBuiltinPlugin : IDashSpecPlugin, IDashSpecEndpointContributor
{
    public string Id => "dashspec_diagnostics";

    public string DisplayName => "DashSpec load diagnostics";

    public PluginTier Tier => PluginTier.Extended;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<LoadDiagnosticsService>();
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/diagnostics/load", (
            LoadDiagnosticsService diagnostics,
            string? entry,
            bool cards = false,
            bool fieldOptions = true) =>
        {
            var report = string.IsNullOrWhiteSpace(entry)
                ? diagnostics.DiagnoseConfiguredSpec(cards, fieldOptions)
                : diagnostics.DiagnoseCatalogEntry(entry, cards, fieldOptions);

            return report.Success
                ? Results.Json(report)
                : Results.Json(report, statusCode: StatusCodes.Status502BadGateway);
        });

        endpoints.MapGet("/diagnostics/load/ping", async (
            LoadDiagnosticsService diagnostics,
            CancellationToken cancellationToken) =>
            Results.Json(await diagnostics.PingConnectorAsync(cancellationToken).ConfigureAwait(false)));

        endpoints.MapGet("/diagnostics/load/last", (LoadTrace trace) =>
        {
            var last = trace.Last;
            return last is null
                ? Results.Json(new { message = "No UI load recorded yet." })
                : Results.Json(last);
        });

        endpoints.MapGet("/diagnostics/load/history", (LoadTrace trace) => Results.Json(trace.History));

        endpoints.MapGet("/diagnostics/capabilities", (DashSpecPluginCapabilities capabilities) =>
            Results.Json(capabilities));
    }
}
