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
        if (!TryResolveRunnable(line, catalog, out var resolution, out var error))
        {
            return CommandOutcome.Fail(error ?? "Unknown command.");
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

    public bool TryValidateRunnable(
        string line,
        SlashCatalogIndex catalog,
        out string? error)
    {
        if (TryResolveRunnable(line, catalog, out _, out error))
        {
            error = null;
            return true;
        }

        return false;
    }

    static bool TryResolveRunnable(
        string line,
        SlashCatalogIndex catalog,
        out SlashLineResolver.SlashLineResolution resolution,
        out string? error)
    {
        resolution = default!;
        error = null;
        var slashLine = line.Trim();
        if (!slashLine.StartsWith('/'))
        {
            slashLine = "/" + slashLine;
        }

        if (!SlashLineResolver.TryResolveSlashLine(slashLine, catalog, out var resolved))
        {
            error = "Неизвестная команда.";
            return false;
        }

        if (!resolved.IsRunnable || NeedsArgument(resolved))
        {
            error = MissingArgsMessage(resolved, catalog);
            return false;
        }

        resolution = resolved;
        return true;
    }

    static bool NeedsArgument(SlashLineResolver.SlashLineResolution resolution) =>
        resolution.ArgTailKind switch
        {
            SlashArgTailKind.Required or SlashArgTailKind.Picker =>
                !resolution.HasArgTailContent,
            _ => false,
        };

    static string MissingArgsMessage(
        SlashLineResolver.SlashLineResolution resolution,
        SlashCatalogIndex catalog)
    {
        if (!catalog.TryGet(resolution.CanonicalPath, out var route))
        {
            return "Команда неполная — укажите аргумент.";
        }

        return route.ArgTailKind switch
        {
            SlashArgTailKind.Picker => "Выберите значение (Tab) или допишите аргумент.",
            SlashArgTailKind.Required => "Допишите значение аргумента.",
            SlashArgTailKind.Optional => "Добавьте аргумент или завершите команду.",
            _ => "Команда неполная — укажите аргумент.",
        };
    }
}
