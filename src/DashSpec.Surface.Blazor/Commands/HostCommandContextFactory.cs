#nullable enable
using DashSpec.Core.Model;
using DashSpec.Surface.Blazor.Services.Presentation;

namespace DashSpec.Surface.Blazor.Commands;

internal static class HostCommandContextFactory
{
    public static DashboardFilterContext CreateHostOnly(
        DashboardFilterUiState uiState,
        IDashboardCultureAmbient culture) =>
        new()
        {
            ReportId = "host",
            ActiveScope = [DashSpecCommandScope.ControlCenter],
            FilterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase),
            ToolbarFilterNames = [],
            CommandAliases = DashSpec.Modeling.Parse.Document.DashboardDocumentModule.emptyCommandAliases,
            UiState = uiState,
            GetFieldOptions = _ => [],
            Culture = culture.Culture,
        };
}
