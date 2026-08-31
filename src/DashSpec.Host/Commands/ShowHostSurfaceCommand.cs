#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal sealed class ShowHostSurfaceCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.show.surface";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var surfaceId = context.ArgTail.Trim();
        if (surfaceId.Length == 0)
        {
            surfaceId = ShowCommandPaths.ReadSurfaceId(context.CanonicalPath) ?? "";
        }

        if (surfaceId.Length == 0)
        {
            return CommandOutcome.Fail("Укажите вкладку: show dashboard | controlcenter.");
        }

        if (!HostSurfaceCatalog.TryResolveRoute(surfaceId, out var route))
        {
            return CommandOutcome.Fail($"Неизвестная вкладка '{surfaceId}'.");
        }

        context.PendingHostRoute = route;
        return CommandOutcome.Ok();
    }
}
