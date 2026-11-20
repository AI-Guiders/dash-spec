#nullable enable

using DashSpec.Plugin.Studio.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DashSpec.Plugin.Studio.Endpoints;

public sealed record StudioUpdateDraftRequest(
    string AuthorId,
    string AuthorName,
    string RelativePath,
    string Text);

public sealed record StudioPublishRequest(
    string AuthorId,
    string AuthorName,
    bool CommitToGit = false,
    string? CommitMessage = null,
    bool ForceOverwriteConflicts = false);

public sealed record StudioLspPositionRequest(
    string RelativePath,
    string Text,
    int LineNumber,
    int Column);

public sealed record StudioLspRenameRequest(
    string RelativePath,
    string Text,
    int LineNumber,
    int Column,
    string NewName);

public static class StudioEndpoints
{
    public static IEndpointRouteBuilder MapDashSpecStudioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/studio");

        group.MapGet("/files", (StudioWorkspaceService workspace) =>
        {
            var index = workspace.GetWorkspaceIndex();
            return Results.Ok(new
            {
                workspaceRoot = workspace.ResolveWorkspaceRoot(),
                units = index.Units.Select(u => new
                {
                    u.Kind,
                    u.Id,
                    u.Namespace,
                    u.Line,
                    relativePath = workspace.ToRelativePath(u.Path),
                }),
            });
        });

        group.MapGet("/inspect", async (
            string path,
            string? authorId,
            string? authorName,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
        {
            var inspection = await workspace.InspectDocumentAsync(
                string.IsNullOrWhiteSpace(authorId) ? "api-author" : authorId,
                string.IsNullOrWhiteSpace(authorName) ? "API Author" : authorName,
                path,
                ct);
            return Results.Ok(new
            {
                inspection.RelativePath,
                inspection.Unit,
                inspection.DiskHash,
                inspection.BaseHash,
                inspection.IsDraftModified,
                inspection.HasUpstreamConflict,
                fileDiagnosticsCount = inspection.FileDiagnostics.Count,
                fileDiagnostics = StudioGraphAndLspBuilder.BuildMonacoMarkers(inspection),
                assemblyHintsCount = inspection.AssemblyHints.Count,
                nodesCount = inspection.Snapshot.Nodes.Count,
                wiringNodesCount = inspection.WiringGraph?.Nodes.Count ?? 0,
                wiringEdgesCount = inspection.WiringGraph?.Edges.Length ?? 0,
                flowNodesCount = inspection.FlowGraph?.Nodes.Count ?? 0,
                flowEdgesCount = inspection.FlowGraph?.Edges.Length ?? 0,
                resolvedCardsCount = inspection.ResolvedReport?.Cards.Count ?? 0,
                inspection.ResolveHint,
            });
        });

        group.MapPost("/draft", async (
            StudioUpdateDraftRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
        {
            var inspection = await workspace.UpdateDraftAsync(req.AuthorId, req.AuthorName, req.RelativePath, req.Text, ct);
            return Results.Ok(new
            {
                inspection.RelativePath,
                inspection.IsDraftModified,
                inspection.HasUpstreamConflict,
                fileDiagnostics = StudioGraphAndLspBuilder.BuildMonacoMarkers(inspection),
                nodesCount = inspection.Snapshot.Nodes.Count,
            });
        });

        group.MapPost("/lsp/diagnostics", async (
            StudioLspPositionRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
        {
            var diags = await workspace.EvaluateDiagnosticsOnlyAsync(req.RelativePath, req.Text, ct);
            return Results.Ok(diags);
        });

        group.MapPost("/lsp/completions", async (
            StudioLspPositionRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await StudioGraphAndLspBuilder.GetCompletionsAsync(
                workspace,
                req.RelativePath,
                req.Text,
                req.LineNumber,
                req.Column,
                ct)));

        group.MapPost("/lsp/definition", async (
            StudioLspPositionRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await StudioGraphAndLspBuilder.ResolveDefinitionAsync(
                workspace,
                req.RelativePath,
                req.Text,
                req.LineNumber,
                req.Column,
                ct)));

        group.MapPost("/lsp/references", async (
            StudioLspPositionRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await StudioGraphAndLspBuilder.FindReferencesAsync(
                workspace,
                req.RelativePath,
                req.Text,
                req.LineNumber,
                req.Column,
                ct)));

        group.MapPost("/lsp/hover", async (
            StudioLspPositionRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
            Results.Ok(await StudioGraphAndLspBuilder.BuildHoverInfoAsync(
                workspace,
                req.RelativePath,
                req.Text,
                req.LineNumber,
                req.Column,
                ct)));

        group.MapPost("/lsp/rename", async (
            StudioLspRenameRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
        {
            var root = workspace.ResolveWorkspaceRoot();
            var fullPath = Path.GetFullPath(Path.Combine(root, req.RelativePath));
            var langReq = new AIGuiders.Platform.Modeling.Language.LanguageRequest(
                fullPath,
                req.LineNumber,
                req.Column,
                req.Text,
                root,
                AIGuiders.Platform.Modeling.Language.LanguageIds.Dashspec);
            var res = await workspace.LanguageBackend.RenameSymbolAsync(
                new AIGuiders.Platform.Modeling.Language.RenameSymbolRequest(langReq, req.NewName, false),
                ct);
            return Results.Ok(new
            {
                res.OldName,
                res.NewName,
                res.SymbolKind,
                res.Message,
                newText = res.Changes.Length > 0 ? res.Changes[0].NewText : req.Text,
            });
        });

        group.MapPost("/lsp/symbols", (StudioLspPositionRequest req) =>
            Results.Ok(StudioGraphAndLspBuilder.BuildMonacoSymbols(req.RelativePath, req.Text)));

        group.MapPost("/lsp/semantic-tokens", (StudioLspPositionRequest req) =>
            Results.Ok(new
            {
                data = StudioGraphAndLspBuilder.BuildMonacoSemanticTokensData(req.Text),
            }));

        group.MapPost("/publish", async (
            StudioPublishRequest req,
            StudioWorkspaceService workspace,
            CancellationToken ct) =>
        {
            var outcome = await workspace.PublishAuthorDraftsAsync(
                req.AuthorId,
                req.AuthorName,
                req.CommitToGit,
                req.CommitMessage,
                req.ForceOverwriteConflicts,
                ct);
            return Results.Ok(outcome);
        });

        group.MapGet("/presence", (StudioPresenceTracker presence) =>
            Results.Ok(presence.GetActiveAuthors()));

        return endpoints;
    }
}
