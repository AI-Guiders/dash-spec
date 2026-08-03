using DashSpec.Host.Configuration;
using DashSpec.Host.Security;

namespace DashSpec.Host.Middleware;

/// <summary>
/// X-Api-Key header, dashspec-access cookie, or one-time ?api_key= query (sets cookie).
/// /health and /access are open; static assets for Blazor shell are open.
/// </summary>
public sealed class DashSpecAccessMiddleware(
    RequestDelegate next,
    DashSpecAccessValidator validator)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!validator.IsRequired)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path;

        if (IsAnonymousPath(path))
        {
            await next(context);
            return;
        }

        if (TryGetProvidedKey(context, out var provided) && validator.Validate(provided))
        {
            if (context.Request.Query.ContainsKey(DashSpecAccessOptions.QueryName))
            {
                SetAccessCookie(context, provided!);
                var clean = BuildPathWithoutApiKey(context);
                context.Response.Redirect(clean);
                return;
            }

            await next(context);
            return;
        }

        if (WantsHtml(context))
        {
            var returnUrl = Uri.EscapeDataString(path + context.Request.QueryString);
            context.Response.Redirect($"/access?returnUrl={returnUrl}");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Invalid or missing API key.");
    }

    private static bool IsAnonymousPath(PathString path)
    {
        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/access", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Value is "/favicon.ico";
    }

    private static bool TryGetProvidedKey(HttpContext context, out string? key)
    {
        if (context.Request.Headers.TryGetValue(DashSpecAccessOptions.HeaderName, out var header) &&
            !string.IsNullOrWhiteSpace(header))
        {
            key = header.ToString();
            return true;
        }

        if (context.Request.Cookies.TryGetValue(DashSpecAccessOptions.CookieName, out var cookie) &&
            !string.IsNullOrWhiteSpace(cookie))
        {
            key = cookie;
            return true;
        }

        if (context.Request.Query.TryGetValue(DashSpecAccessOptions.QueryName, out var query) &&
            !string.IsNullOrWhiteSpace(query))
        {
            key = query.ToString();
            return true;
        }

        key = null;
        return false;
    }

    private void SetAccessCookie(HttpContext context, string apiKey)
    {
        context.Response.Cookies.Append(
            DashSpecAccessOptions.CookieName,
            apiKey,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(30),
                Path = "/",
                IsEssential = true,
            });
    }

    private static string BuildPathWithoutApiKey(HttpContext context)
    {
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        var filtered = query
            .Where(pair => !string.Equals(pair.Key, DashSpecAccessOptions.QueryName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var path = context.Request.Path.Value ?? "/";
        var rebuilt = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(path, filtered);
        return string.IsNullOrEmpty(rebuilt) ? "/" : rebuilt;
    }

    private static bool WantsHtml(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            var accept = context.Request.Headers.Accept.ToString();
            return string.IsNullOrWhiteSpace(accept) ||
                   accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
