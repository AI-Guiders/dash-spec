#nullable enable
using AIGuiders.Platform.CommandPlane;
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

public sealed class DashboardCommandExecutor(DashSpecCommandPluginRegistry pluginRegistry)
{
    public CommandOutcome TryExecuteSlashLine(
        string line,
        DashboardFilterContext context,
        SlashCatalogIndex catalog)
    {
        var slashLine = line.Trim();
        if (!slashLine.StartsWith('/'))
        {
            slashLine = "/" + slashLine;
        }

        if (!SlashLineResolver.TryResolveSlashLine(slashLine, catalog, out var resolution))
        {
            return CommandOutcome.Fail("Unknown command.");
        }

        if (!resolution.IsRunnable)
        {
            return CommandOutcome.Fail("Command is missing required arguments.");
        }

        if (!catalog.TryGet(resolution.CanonicalPath, out var route))
        {
            return CommandOutcome.Fail($"Unknown command path '{resolution.CanonicalPath}'.");
        }

        context.CanonicalPath = resolution.CanonicalPath;
        context.ArgTail = resolution.ArgTail;

        var registry = DashboardCommandRegistryFactory.Create(
            DashboardCommandAliasResolver.ResolveFieldSlashAliases(context),
            pluginRegistry.Commands);

        if (!registry.TryExecute(route.CommandId, context, out var outcome))
        {
            return outcome.Success
                ? outcome
                : CommandOutcome.Fail(outcome.Error ?? $"Command failed: {route.CommandId}");
        }

        return outcome;
    }
}
