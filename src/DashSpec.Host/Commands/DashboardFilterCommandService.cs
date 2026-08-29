#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Commands;

public sealed class DashboardFilterCommandService(
    IDashboardSession session,
    DashboardFilterUiState uiState,
    DashboardCommandExecutor executor,
    DashSpecContributorRegistry pluginRegistry)
{
    public SlashCatalogIndex BuildCatalog(IReadOnlyList<string> toolbarFilterNames)
    {
        var context = CreateContext(toolbarFilterNames);
        return DashboardCommandCatalogBuilder.Build(context, pluginRegistry.CommandDescriptors);
    }

    public IReadOnlyList<SlashCompletionItem> GetSuggestions(
        string typedBody,
        IReadOnlyList<string> toolbarFilterNames) =>
        SlashStepCompletion.GetSuggestions(
            BuildCatalog(toolbarFilterNames),
            typedBody,
            CreatePickerSource(toolbarFilterNames));

    public CommandOutcome TryExecute(string line, IReadOnlyList<string> toolbarFilterNames)
    {
        var context = CreateContext(toolbarFilterNames);
        var catalog = DashboardCommandCatalogBuilder.Build(context, pluginRegistry.CommandDescriptors);
        return executor.TryExecuteSlashLine(line, context, catalog);
    }

    DashboardFilterContext CreateContext(IReadOnlyList<string> toolbarFilterNames) =>
        new()
        {
            ReportId = session.Document.Id,
            FilterIndex = session.FilterIndex,
            ToolbarFilterNames = toolbarFilterNames,
            CommandAliases = session.Document.ResolvedCommandAliases,
            UiState = uiState,
            GetFieldOptions = session.GetFieldOptions,
        };

    ISlashPickerChoiceSource CreatePickerSource(IReadOnlyList<string> toolbarFilterNames) =>
        new DashboardFilterPickerSource(session, toolbarFilterNames);
}
