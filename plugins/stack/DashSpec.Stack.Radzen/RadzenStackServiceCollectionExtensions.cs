using DashSpec.Abstractions.Viz;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Stack.Radzen;

public static class RadzenStackServiceCollectionExtensions
{
    /// <summary>Register the Radzen render stack (ADR-0112): the Radzen visual language.</summary>
    public static IServiceCollection AddRadzenRenderStack(this IServiceCollection services)
    {
        // Ensure DashSpec.Plugin.Viz.Radzen is loaded so plugin discovery registers it.
        _ = typeof(DashSpec.Plugin.Viz.RadzenUI.Plugins.RadzenVizDashSpecPlugin).Assembly;
        return services.AddSingleton<IRenderStack, RadzenRenderStack>();
    }
}
