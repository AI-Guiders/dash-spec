#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal sealed class SelectDateFilterCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.select.date";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var filterName = DashboardCommandAliasResolver.ResolveDateFilter(context);
        if (filterName is null)
        {
            return CommandOutcome.Fail("No date filter on toolbar.");
        }

        if (!DateFilterPresets.TryResolve(context.ArgTail, context.TodayUtc, out var range, out var error))
        {
            return CommandOutcome.Fail(error ?? "Invalid date argument.");
        }

        context.ApplyDate(filterName, range.From, range.To);
        return CommandOutcome.Ok();
    }
}
