namespace DashSpec.Host.Services.Presentation;

/// <summary>Notifies Blazor chrome when WitDB presentation settings change at runtime.</summary>
public sealed class HostPresentationSignals
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
