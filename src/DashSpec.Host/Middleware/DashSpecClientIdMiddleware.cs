using DashSpec.Host.Services.Settings;

namespace DashSpec.Host.Middleware;

/// <summary>
/// Sets <see cref="CatalogUsageService.ClientCookieName"/> on the initial HTTP response
/// (before Blazor interactive circuit runs — there headers are already committed).
/// </summary>
public sealed class DashSpecClientIdMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, CatalogUsageService catalogUsage)
    {
        catalogUsage.EnsureClientCookie(context);
        return next(context);
    }
}
