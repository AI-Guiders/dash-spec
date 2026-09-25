using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Abstractions;

public interface IHostSettingsOverlay
{
    void Apply(DashSpecTomlRoot bootstrap);
}
