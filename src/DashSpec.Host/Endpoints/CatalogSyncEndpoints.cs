using System.Text.Json;
using DashSpec.Host.Configuration;
using DashSpec.Host.Security;
using DashSpec.Host.Services.Git;

namespace DashSpec.Host.Endpoints;

internal static class CatalogSyncEndpoints
{
    public static void MapCatalogSyncEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/admin/catalog/sync", SyncCatalog)
            .DisableAntiforgery();
    }

    private static async Task<IResult> SyncCatalog(
        HttpContext context,
        DashSpecTomlRoot bootstrap,
        DashSpecAccessOptions accessOptions,
        GitCatalogSyncService syncService,
        CancellationToken cancellationToken)
    {
        context.Request.EnableBuffering();
        using var ms = new MemoryStream();
        await context.Request.Body.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        var rawBody = ms.ToArray();

        if (!CatalogSyncWebhookAuth.TryAuthorize(
                context.Request,
                rawBody,
                bootstrap.CatalogGit,
                accessOptions.ApiKey,
                out var authStatus,
                out var authError))
        {
            return Results.Json(new { status = "error", error = authError }, statusCode: authStatus);
        }

        if (!syncService.IsEnabled)
        {
            return Results.Json(
                new { status = "disabled", error = "catalog_git is not enabled." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!TryMatchRepoFilter(rawBody, bootstrap.CatalogGit, out var skipReason))
        {
            return Results.Json(new { status = "skipped", reason = skipReason });
        }

        var result = await syncService.SyncAsync(cancellationToken).ConfigureAwait(false);
        return Results.Json(
            new
            {
                status = result.Status,
                changed = result.Changed,
                catalogPath = result.CatalogPath,
            },
            statusCode: result.HttpStatus);
    }

    private static bool TryMatchRepoFilter(byte[] rawBody, CatalogGitTomlSection git, out string? skipReason)
    {
        skipReason = null;
        var expectedRepo = git.SyncRepoSlug?.Trim();
        var expectedBranch = string.IsNullOrWhiteSpace(git.Branch) ? "main" : git.Branch.Trim();

        if (rawBody.Length == 0)
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var repo = ReadRepo(root);
            var branch = ReadBranch(root);

            if (!string.IsNullOrEmpty(expectedRepo)
                && !string.IsNullOrEmpty(repo)
                && !string.Equals(repo, expectedRepo, StringComparison.OrdinalIgnoreCase))
            {
                skipReason = $"repo '{repo}' does not match sync_repo_slug '{expectedRepo}'";
                return false;
            }

            if (!string.IsNullOrEmpty(branch)
                && !string.Equals(branch, expectedBranch, StringComparison.OrdinalIgnoreCase))
            {
                skipReason = $"branch '{branch}' does not match catalog_git.branch '{expectedBranch}'";
                return false;
            }
        }
        catch (JsonException)
        {
            // Non-JSON body: still allow sync (manual trigger).
        }

        return true;
    }

    private static string? ReadRepo(JsonElement root)
    {
        if (root.TryGetProperty("repo", out var repo))
        {
            return repo.GetString();
        }

        if (root.TryGetProperty("refs", out var refs)
            && refs.TryGetProperty("repo", out var refsRepo))
        {
            return refsRepo.GetString();
        }

        return null;
    }

    private static string? ReadBranch(JsonElement root)
    {
        if (root.TryGetProperty("branch", out var branch))
        {
            return branch.GetString();
        }

        if (root.TryGetProperty("payload", out var payload)
            && payload.TryGetProperty("branch", out var payloadBranch))
        {
            return payloadBranch.GetString();
        }

        if (root.TryGetProperty("ref", out var refEl))
        {
            var value = refEl.GetString();
            if (!string.IsNullOrEmpty(value) && value.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                return value["refs/heads/".Length..];
            }
        }

        if (root.TryGetProperty("payload", out var payload2)
            && payload2.TryGetProperty("ref", out var payloadRef))
        {
            var value = payloadRef.GetString();
            if (!string.IsNullOrEmpty(value) && value.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                return value["refs/heads/".Length..];
            }
        }

        return null;
    }
}
