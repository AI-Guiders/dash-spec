#nullable enable
using AIGuiders.Platform.CommandPlane;
using DashSpec.Host.Commands;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Presentation;
using Microsoft.AspNetCore.Mvc;

namespace DashSpec.Host.Endpoints;

public static class DashboardCommandEndpoints
{
    public static WebApplication MapDashboardCommandEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/commands/complete", Complete);
        app.MapGet("/api/v1/commands/capabilities", Capabilities);
        app.MapPost("/api/v1/commands/execute", Execute);
        return app;
    }

    static IResult Complete(
        HttpContext context,
        DashboardFilterCommandService commands,
        IDashboardSession session,
        DashboardFilterUiState uiState)
    {
        var line = context.Request.Query["line"].ToString();
        var commandContext = BuildApiContext(session, ResolveToolbarFilters(context, session), uiState);
        var result = commands.GetCompletionResult(line, commandContext);
        return Results.Ok(new
        {
            items = result.Items.Select(i => new
            {
                insertText = i.InsertText,
                path = i.CommandPath,
                help = i.Help,
                group = i.Group,
                stepSegment = i.StepSegment,
                kind = i.Kind.ToString().ToLowerInvariant(),
                pickValue = i.PickValue,
            }),
            guidance = new
            {
                mode = result.Guidance.Mode.ToString().ToLowerInvariant(),
                breadcrumb = result.Guidance.Breadcrumb,
                placeholder = result.Guidance.Placeholder,
                hint = result.Guidance.Hint,
                canonicalPath = result.Guidance.CanonicalPath,
                argTailKind = result.Guidance.ArgTailKind,
            },
        });
    }

    static IResult Capabilities(
        DashboardFilterCommandService commands,
        IDashboardSession session,
        DashboardFilterUiState uiState,
        HttpContext context)
    {
        var commandContext = BuildApiContext(session, ResolveToolbarFilters(context, session), uiState);
        var catalog = commands.BuildCatalog(commandContext);
        return Results.Ok(new
        {
            commands = catalog.Routes
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Select(route => new
                {
                    commandId = route.CommandId,
                    path = route.Path,
                    help = route.Help,
                    group = route.Group,
                    argTail = route.ArgTailKind.ToString(),
                }),
        });
    }

    static IResult Execute(
        [FromBody] DashboardCommandExecuteRequest request,
        DashboardFilterCommandService commands,
        DashboardFilterUiState uiState,
        IDashboardSession session)
    {
        if (string.IsNullOrWhiteSpace(request.Line))
        {
            return Results.BadRequest(new { error = "line is required." });
        }

        var toolbar = request.ToolbarFilters?.Count > 0
            ? request.ToolbarFilters
            : session.Document.DashboardFilters;

        uiState.LoadFromSession(session, toolbar);
        var commandContext = BuildApiContext(session, toolbar, uiState);
        var run = commands.TryExecute(request.Line, commandContext);
        if (!run.Outcome.Success)
        {
            return Results.BadRequest(new { success = false, error = run.Outcome.Error });
        }

        uiState.SyncToSession(session, toolbar);
        return Results.Ok(new { success = true });
    }

    static DashboardFilterContext BuildApiContext(
        IDashboardSession session,
        IReadOnlyList<string> toolbar,
        DashboardFilterUiState uiState) =>
        new()
        {
            ReportId = session.Document.Id,
            FilterIndex = session.FilterIndex,
            ToolbarFilterNames = toolbar,
            CommandAliases = session.Document.ResolvedCommandAliases,
            UiState = uiState,
            GetFieldOptions = session.GetFieldOptions,
            CatalogEntries = [],
            ReportPages = session.Document.Pages ?? [],
            ActiveCatalogEntryId = session.ActiveCatalogEntryId,
        };

    static IReadOnlyList<string> ResolveToolbarFilters(HttpContext context, IDashboardSession session)
    {
        var raw = context.Request.Query["toolbar"].ToString();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        try
        {
            return session.Document.DashboardFilters;
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    public sealed record DashboardCommandExecuteRequest(
        string Line,
        IReadOnlyList<string>? ToolbarFilters = null);
}
