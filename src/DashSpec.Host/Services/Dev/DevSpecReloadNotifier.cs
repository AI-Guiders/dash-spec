namespace DashSpec.Host.Services.Dev;

public sealed class DevSpecReloadNotifier
{
    public event Action? Changed;

    public void Notify() => Changed?.Invoke();
}
