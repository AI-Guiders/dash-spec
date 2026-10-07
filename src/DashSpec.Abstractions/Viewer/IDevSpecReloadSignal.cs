namespace DashSpec.Abstractions.Viewer;

/// <summary>Dev hot-reload notifications when local .dashspec changes.</summary>
public interface IDevSpecReloadSignal
{
    event Action? Changed;
}
