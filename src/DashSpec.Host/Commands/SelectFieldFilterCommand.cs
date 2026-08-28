#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;
using DashSpec.Core.Model;

namespace DashSpec.Host.Commands;

internal sealed class SelectFieldFilterCommand(string slashAlias) : PlatformCommand<DashboardFilterContext>
{
    public override string CommandId => $"dash.select.{slashAlias}";

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var filterName = DashboardCommandAliasResolver.ResolveFieldFilter(slashAlias, context);
        if (filterName is null)
        {
            return CommandOutcome.Fail($"Unknown field alias '{slashAlias}'.");
        }

        if (!context.FilterIndex.TryGetValue(filterName, out var filter) ||
            filter.Kind is not FilterKind.Field)
        {
            return CommandOutcome.Fail($"Filter '{filterName}' is not a field filter.");
        }

        var options = context.GetFieldOptions(filterName);
        if (!FieldFilterValueResolver.TryResolveValues(
                context.ArgTail,
                filter,
                options,
                out var values,
                out var error))
        {
            return CommandOutcome.Fail(error ?? "Invalid field value.");
        }

        context.ApplyField(filterName, values);
        return CommandOutcome.Ok();
    }
}
