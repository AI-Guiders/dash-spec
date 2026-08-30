#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Core.Model;

namespace DashSpec.Host.Commands;

internal sealed class SelectFieldFilterCommand(string filterName) : PlatformCommand<DashboardFilterContext>
{
    public override string CommandId => $"dash.select.filter.{filterName}";

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var resolvedName = FilterCommandPaths.ReadFilterName(context.CanonicalPath) ?? filterName;
        if (!context.FilterIndex.TryGetValue(resolvedName, out var filter)
            || filter.Kind is not FilterKind.Field)
        {
            return CommandOutcome.Fail($"Фильтр '{resolvedName}' не найден.");
        }

        if (!context.ToolbarFilterNames.Contains(resolvedName, StringComparer.OrdinalIgnoreCase))
        {
            return CommandOutcome.Fail($"Фильтр '{resolvedName}' недоступен на toolbar.");
        }

        var options = context.GetFieldOptions(resolvedName);
        if (!FieldFilterValueResolver.TryResolveValues(
                context.ArgTail,
                filter,
                options,
                out var values,
                out var error))
        {
            return CommandOutcome.Fail(error ?? "Некорректное значение.");
        }

        context.ApplyField(resolvedName, values);
        return CommandOutcome.Ok();
    }
}
