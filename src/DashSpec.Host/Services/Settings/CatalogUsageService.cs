using DashSpec.Host.Data;
using Microsoft.EntityFrameworkCore;

namespace DashSpec.Host.Services.Settings;

/// <summary>Remark 31: per-browser catalog entry usage for default report selection.</summary>
public sealed class CatalogUsageService(DashSpecHostDbContext db)
{
    public const string ClientCookieName = "dashspec_client_id";

    private const string SessionItemsKey = "__dashspec_client_id";

    public string GetOrCreateClientId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return "anonymous";
        }

        if (TryReadClientId(httpContext, out var existing))
        {
            return existing;
        }

        var clientId = Guid.NewGuid().ToString("N");
        httpContext.Items[SessionItemsKey] = clientId;
        TryWriteClientCookie(httpContext, clientId);
        return clientId;
    }

    /// <summary>Call from HTTP middleware before the response starts (remark 31).</summary>
    public void EnsureClientCookie(HttpContext httpContext)
    {
        if (TryReadClientId(httpContext, out _))
        {
            return;
        }

        var clientId = Guid.NewGuid().ToString("N");
        httpContext.Items[SessionItemsKey] = clientId;
        TryWriteClientCookie(httpContext, clientId);
    }

    private static bool TryReadClientId(HttpContext httpContext, out string clientId)
    {
        if (httpContext.Request.Cookies.TryGetValue(ClientCookieName, out var fromCookie)
            && !string.IsNullOrWhiteSpace(fromCookie))
        {
            clientId = fromCookie;
            return true;
        }

        if (httpContext.Items.TryGetValue(SessionItemsKey, out var cached)
            && cached is string fromItems
            && !string.IsNullOrWhiteSpace(fromItems))
        {
            clientId = fromItems;
            return true;
        }

        clientId = string.Empty;
        return false;
    }

    private static void TryWriteClientCookie(HttpContext httpContext, string clientId)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        httpContext.Response.Cookies.Append(
            ClientCookieName,
            clientId,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(400),
                SameSite = SameSiteMode.Lax,
            });
    }

    public string? ResolvePreferredEntryId(string clientId, string catalogDefaultEntryId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var best = db.CatalogUsage.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.HitCount)
            .ThenByDescending(x => x.LastUsedAt)
            .Select(x => x.EntryId)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(best) ? null : best;
    }

    public void RecordSelection(string clientId, string entryId)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(entryId))
        {
            return;
        }

        var row = db.CatalogUsage.FirstOrDefault(x => x.ClientId == clientId && x.EntryId == entryId);
        if (row is null)
        {
            db.CatalogUsage.Add(new CatalogUsageEntity
            {
                ClientId = clientId,
                EntryId = entryId,
                HitCount = 1,
                LastUsedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            row.HitCount++;
            row.LastUsedAt = DateTimeOffset.UtcNow;
        }

        db.SaveChanges();
    }
}
