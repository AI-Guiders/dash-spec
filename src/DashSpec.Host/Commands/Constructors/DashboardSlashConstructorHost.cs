#nullable enable

using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands.Constructors;

public sealed class DashboardSlashConstructorHost
{
    public SlashValueConstructorRegistry Registry { get; } = new();
    public DateConstructorSegmentProvider SegmentProvider { get; } = new();
    public SlashConstructorSession Session { get; }

    public DashboardSlashConstructorHost()
    {
        DateConstructorCatalog.Register(Registry);
        var navigator = new SlashValueConstructorNavigator(Registry, SegmentProvider);
        Session = new SlashConstructorSession(navigator);
    }
}
