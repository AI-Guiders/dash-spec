using DashSpec.Host.Services.Dev;

namespace DashSpec.Host.Endpoints;

internal static class DevSpecEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapGet("/dev/resolve", (DevSpecResolveService resolver) =>
        {
            var result = resolver.ResolveConfiguredSpec();
            return result.Success
                ? Results.Json(result.Export)
                : Results.BadRequest(new { error = result.Error });
        });

        app.MapGet("/dev/resolve/card/{cardId}", (string cardId, DevSpecResolveService resolver) =>
        {
            var result = resolver.ResolveConfiguredSpec();
            if (!result.Success || result.Export is null)
            {
                return Results.BadRequest(new { error = result.Error ?? "Resolve failed." });
            }

            var card = result.Export.Cards.FirstOrDefault(c =>
                string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
            return card is null
                ? Results.NotFound(new { error = $"Card '{cardId}' not found." })
                : Results.Json(card);
        });
    }
}
