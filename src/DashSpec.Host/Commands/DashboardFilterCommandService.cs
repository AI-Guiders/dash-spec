#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.ArgSuggestions;
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
    DashSpecCommandPluginRegistry commandPluginRegistry,
    DashboardSlashConstructorHost constructorHost)
{
    public CommandCatalogIndex BuildCatalog(DashboardFilterContext context) =>
        DashboardCommandCatalogBuilder.Build(
            context,
            pluginRegistry.CommandDescriptors,
            commandPluginRegistry.Commands);

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
            CreateSuggestionBroker(context),
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
            context.PendingViewId,
            context.PendingHostRoute);
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

    ICommandArgSuggestionBroker CreateSuggestionBroker(DashboardFilterContext context) =>
        new CommandArgSuggestionRegistry()
            .RegisterPrefix("dash.field.", new DashboardFilterSuggestionProvider(session, context.ToolbarFilterNames))
            .Build();
}
