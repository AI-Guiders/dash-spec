using DashSpec.Abstractions.Viz;
using DashSpec.Surface.Blazor.Services.Localization;

namespace DashSpec.Surface.Blazor.Services.Localization;

public sealed class VizCardToolbarLocalizer(DashboardLocalizer dashboardLocalizer) : IVizCardToolbarLocalizer
{
    public string T(string key) => dashboardLocalizer.T(key);
}
