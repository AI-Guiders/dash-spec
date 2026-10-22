using DashSpec.Abstractions.Viz;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Stack.SelfMade;

public static class SelfMadeStackServiceCollectionExtensions
{
    /// <summary>Register the SelfMade render stack (ADR-0112): the pre-Radzen visual language.</summary>
    public static IServiceCollection AddSelfMadeRenderStack(this IServiceCollection services) =>
        services.AddSingleton<IRenderStack, SelfMadeRenderStack>();
}
