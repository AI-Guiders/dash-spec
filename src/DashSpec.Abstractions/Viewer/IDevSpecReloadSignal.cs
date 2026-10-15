namespace DashSpec.Abstractions.Viewer;

/// <summary>Dev hot-reload notifications when a local `.ds` spec changes.</summary>
public interface IDevSpecReloadSignal
{
    event Action? Changed;
}
