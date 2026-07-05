using Microsoft.AspNetCore.Routing;

namespace DashSpec.Abstractions.Plugins;

/// <summary>Optional HTTP surface for authoring / ops tooling (diagnostics, capabilities, …).</summary>
public interface IDashSpecEndpointContributor
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
