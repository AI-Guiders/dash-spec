using System.Net;
using System.Text;
using DashSpec.Host.Configuration;
using DashSpec.Host.Security;
using Microsoft.AspNetCore.Mvc;

namespace DashSpec.Host.Endpoints;

internal static class AccessEndpoints
{
    public static void MapAccessEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "ok", service = "dashspec-host" }));

        app.MapGet("/access", (HttpContext ctx, [FromQuery] string? returnUrl, [FromQuery] string? error) =>
        {
            var safeReturn = SanitizeReturnUrl(returnUrl);
            return Results.Content(BuildLoginHtml(safeReturn, error), "text/html; charset=utf-8");
        });

        app.MapPost("/access", async (
            HttpContext ctx,
            DashSpecAccessValidator validator,
            IHostEnvironment environment,
            [FromForm] string? api_key,
            [FromForm] string? returnUrl) =>
        {
            if (!validator.IsRequired)
            {
                ctx.Response.Redirect(SanitizeReturnUrl(returnUrl));
                return;
            }

            if (!validator.Validate(api_key))
            {
                var err = Uri.EscapeDataString("Неверный ключ доступа.");
                var ret = Uri.EscapeDataString(SanitizeReturnUrl(returnUrl));
                ctx.Response.Redirect($"/access?error={err}&returnUrl={ret}");
                return;
            }

            ctx.Response.Cookies.Append(
                DashSpecAccessOptions.CookieName,
                api_key!,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = !environment.IsDevelopment(),
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(30),
                    Path = "/",
                    IsEssential = true,
                });

            ctx.Response.Redirect(SanitizeReturnUrl(returnUrl));
        }).DisableAntiforgery();
    }

    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    private static string BuildLoginHtml(string returnUrl, string? error)
    {
        var encodedReturn = WebUtility.HtmlEncode(returnUrl);
        var errorBlock = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""<p class="error" role="alert">{WebUtility.HtmlEncode(error)}</p>""";

        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>DashSpec — доступ</title>
              <style>
                :root { color-scheme: dark; --bg:#0f1419; --surface:#1a2332; --border:#2d3a4d; --text:#e8eef5; --muted:#8fa3b8; --accent:#5b9fd4; --danger:#e57373; }
                * { box-sizing: border-box; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; font-family: system-ui, sans-serif; background: var(--bg); color: var(--text); }
                .card { width: min(22rem, 92vw); padding: 1.5rem; background: var(--surface); border: 1px solid var(--border); border-radius: 12px; }
                h1 { margin: 0 0 0.35rem; font-size: 1.25rem; }
                p { margin: 0 0 1rem; color: var(--muted); font-size: 0.9rem; line-height: 1.45; }
                label { display: block; font-size: 0.72rem; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; color: var(--muted); margin-bottom: 0.35rem; }
                input[type="password"] { width: 100%; padding: 0.55rem 0.65rem; border-radius: 8px; border: 1px solid var(--border); background: #0f1724; color: var(--text); font-size: 0.95rem; }
                button { margin-top: 1rem; width: 100%; padding: 0.55rem 0.85rem; border: none; border-radius: 8px; background: var(--accent); color: #061018; font-weight: 650; cursor: pointer; }
                button:hover { filter: brightness(1.05); }
                .error { color: var(--danger); margin-bottom: 0.75rem !important; }
                code { font-size: 0.85em; }
              </style>
            </head>
            <body>
              <div class="card">
                <h1>DashSpec</h1>
                <p>Введите ключ доступа (<code>X-Api-Key</code>). Сохранится в cookie браузера на 30 дней.</p>
            """);
        sb.Append(errorBlock);
        sb.Append($"""
                <form method="post" action="/access">
                  <input type="hidden" name="returnUrl" value="{encodedReturn}" />
                  <label for="api_key">Ключ доступа</label>
                  <input id="api_key" name="api_key" type="password" autocomplete="current-password" required autofocus />
                  <button type="submit">Войти</button>
                </form>
              </div>
            </body>
            </html>
            """);
        return sb.ToString();
    }
}
