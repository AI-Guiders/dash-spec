#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Host.Commands.Constructors;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Presentation;

namespace DashSpec.Host.Commands;

public sealed class DashboardFilterCommandService(
    IDashboardSession session,
    DashboardFilterUiState uiState,
    DashboardCommandExecutor executor,
    DashSpecContributorRegistry pluginRegistry,
    DashboardSlashConstructorHost constructorHost)
{
    public SlashCatalogIndex BuildCatalog(DashboardFilterContext context) =>
        DashboardCommandCatalogBuilder.Build(context, pluginRegistry.CommandDescriptors);

    public SlashCompletionResult GetCompletionResult(
        string typedLine,
        DashboardFilterContext context)
    {
        constructorHost.SegmentProvider.Today = context.TodayUtc;
        var catalog = BuildCatalog(context);
        var options = constructorHost.CreateCompletionOptions(context.Culture, context.TodayUtc);
        return DashboardFilterSlashCompletion.GetResult(
            catalog,
            context,
            typedLine,
            CreatePickerSource(context),
            constructorHost.Session,
            options);
    }

    public CommandRunResult TryExecute(string line, DashboardFilterContext context)
    {
        var catalog = BuildCatalog(context);
        var outcome = executor.TryExecuteSlashLine(line, context, catalog);
        return new CommandRunResult(
            outcome,
            context.PendingCatalogEntryId,
            context.PendingPageId,
            context.PendingCardId,
            context.PendingViewId);
    }

    public bool TryValidateRunnable(
        string line,
        DashboardFilterContext context,
        out string? error)
    {
        var catalog = BuildCatalog(context);
        return executor.TryValidateRunnable(line, context, catalog, out error);
    }

    public CommandHighlightState ResolveHighlights(string tail, DashboardFilterContext context) =>
        DashboardCommandHighlightResolver.Resolve(tail, context);

    ISlashPickerChoiceSource CreatePickerSource(DashboardFilterContext context) =>
        new DashboardFilterPickerSource(session, context.ToolbarFilterNames);
}
