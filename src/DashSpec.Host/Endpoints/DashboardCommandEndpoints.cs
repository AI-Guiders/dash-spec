#nullable enable
using AIGuiders.Platform.CommandPlane;
using DashSpec.Host.Commands;

namespace DashSpec.Host.Endpoints;

public static class DashboardCommandEndpoints
{
    public static WebApplication MapDashboardCommandEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/commands/complete", Complete);
        return app;
    }

    static IResult Complete(HttpContext context, DashboardFilterCommandService commands)
    {
        var line = context.Request.Query["line"].ToString();
        var body = NormalizeBody(line);
        var catalog = commands.CurrentCatalog;
        var items = SlashStepCompletion.GetSuggestions(catalog, body);
        return Results.Ok(new
        {
            items = items.Select(i => new
            {
                insertText = i.InsertText,
                path = i.SlashPath,
                help = i.Help,
                group = i.Group,
                stepSegment = i.StepSegment,
            }),
        });
    }

    static string NormalizeBody(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return "";

        var text = line.Trim();
        if (text.StartsWith('/'))
            text = text[1..];
        return text.TrimEnd();
    }
}
