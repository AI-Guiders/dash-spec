#nullable enable
using DashSpec.Core.Model;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Commands;

internal static class HostCommandContextFactory
{
    public static DashboardFilterContext CreateHostOnly(
        DashboardFilterUiState uiState,
        IDashboardCultureAmbient culture) =>
        new()
        {
            ReportId = "host",
            FilterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase),
            ToolbarFilterNames = [],
            CommandAliases = DashboardDocument.EmptyCommandAliases,
            UiState = uiState,
            GetFieldOptions = _ => [],
            Culture = culture.Culture,
        };
}
