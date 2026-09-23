using DashSpec.Host.Data;
using Microsoft.EntityFrameworkCore;

namespace DashSpec.Host.Services.Settings;

/// <summary>Remark 31: per-browser catalog entry usage for default report selection.</summary>
public sealed class CatalogUsageService(DashSpecHostDbContext db)
{
    public const string ClientCookieName = "dashspec_client_id";

    public string GetOrCreateClientId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return "anonymous";
        }

        if (httpContext.Request.Cookies.TryGetValue(ClientCookieName, out var existing)
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var clientId = Guid.NewGuid().ToString("N");
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
        return clientId;
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
