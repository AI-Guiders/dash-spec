using DashSpec.Abstractions.Viz;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Stack.Radzen;

public static class RadzenStackServiceCollectionExtensions
{
    /// <summary>Register the Radzen render stack (ADR-0112): the Radzen visual language.</summary>
    public static IServiceCollection AddRadzenRenderStack(this IServiceCollection services) =>
        services.AddSingleton<IRenderStack, RadzenRenderStack>();
}
