using DashSpec.Abstractions.Viewer;

namespace DashSpec.Host.Configuration;

internal sealed class NullDevSpecReloadSignal : IDevSpecReloadSignal
{
    public event Action? Changed
    {
        add { }
        remove { }
    }
}
