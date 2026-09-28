using DashSpec.Abstractions.Viz;
using DashSpec.Host.Services.Localization;

namespace DashSpec.Host.Services.Localization;

public sealed class VizCardToolbarLocalizer(DashboardLocalizer dashboardLocalizer) : IVizCardToolbarLocalizer
{
    public string T(string key) => dashboardLocalizer.T(key);
}
