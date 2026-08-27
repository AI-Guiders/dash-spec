using System.Security.Cryptography;
using System.Text;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Security;

/// <summary>HMAC / API-key gate for POST /v1/admin/catalog/sync (DASHSPEC-ADR-0041).</summary>
public static class CatalogSyncWebhookAuth
{
    public const string SignatureHeader = "X-Forge-Signature";
    public const string EventHeader = "X-Forge-Event";

    public static bool TryAuthorize(
        HttpRequest request,
        byte[] rawBody,
        CatalogGitTomlSection git,
        string? hostAccessApiKey,
        out int statusCode,
        out string error)
    {
        statusCode = StatusCodes.Status401Unauthorized;
        error = "Unauthorized.";

        var secret = git.SyncWebhookSecret?.Trim();
        var hasSecret = !string.IsNullOrEmpty(secret);

        if (request.Headers.TryGetValue(SignatureHeader, out var signatureHeader)
            && !string.IsNullOrWhiteSpace(signatureHeader))
        {
            if (!hasSecret)
            {
                error = "Signature present but sync_webhook_secret is not configured.";
                return false;
            }

            if (!TryValidateHmac(secret!, rawBody, signatureHeader.ToString()))
            {
                error = "Invalid signature.";
                return false;
            }

            statusCode = StatusCodes.Status200OK;
            error = string.Empty;
            return true;
        }

        if (request.Headers.TryGetValue(DashSpecAccessOptions.HeaderName, out var apiKeyHeader)
            && !string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            var provided = apiKeyHeader.ToString();
            if ((!string.IsNullOrWhiteSpace(hostAccessApiKey)
                    && FixedTimeEqualsUtf8(provided, hostAccessApiKey))
                || (hasSecret && FixedTimeEqualsUtf8(provided, secret!)))
            {
                statusCode = StatusCodes.Status200OK;
                error = string.Empty;
                return true;
            }

            error = "Invalid API key.";
            return false;
        }

        if (git.SyncAllowUnsigned)
        {
            statusCode = StatusCodes.Status200OK;
            error = string.Empty;
            return true;
        }

        if (!hasSecret && string.IsNullOrWhiteSpace(hostAccessApiKey))
        {
            // Dev open host: allow unsigned sync trigger.
            statusCode = StatusCodes.Status200OK;
            error = string.Empty;
            return true;
        }

        error = "Missing X-Forge-Signature or X-Api-Key.";
        return false;
    }

    public static bool TryValidateHmac(string secret, ReadOnlySpan<byte> body, string signatureHeader)
    {
        var value = signatureHeader.Trim();
        if (value.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            value = value["sha256=".Length..];
        }

        var expected = ComputeHmacSha256Hex(secret, body);
        return FixedTimeEqualsUtf8(value, expected);
    }

    public static string ComputeHmacSha256Hex(string secret, ReadOnlySpan<byte> body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(body.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsUtf8(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
