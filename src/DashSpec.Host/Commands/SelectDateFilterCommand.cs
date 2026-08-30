#nullable enable
using AIGuiders.Platform.CommandPlane.Commands;

namespace DashSpec.Host.Commands;

internal sealed class SelectDateFilterCommand : PlatformCommand<DashboardFilterContext>
{
    public const string Id = "dash.select.filter.date";

    public override string CommandId => Id;

    protected override CommandOutcome Execute(DashboardFilterContext context)
    {
        var filterName = FilterCommandPaths.ReadFilterName(context.CanonicalPath);
        if (filterName is null
            || !context.FilterIndex.TryGetValue(filterName, out var filter)
            || filter.Kind is not DashSpec.Core.Model.FilterKind.Date)
        {
            return CommandOutcome.Fail("Неизвестный date-фильтр.");
        }

        if (!context.ToolbarFilterNames.Contains(filterName, StringComparer.OrdinalIgnoreCase))
        {
            return CommandOutcome.Fail($"Фильтр '{filterName}' недоступен на toolbar.");
        }

        if (!DateFilterPresets.TryResolve(context.ArgTail, context.TodayUtc, out var range, out var error))
        {
            return CommandOutcome.Fail(error ?? "Некорректная дата.");
        }

        context.ApplyDate(filterName, range.From, range.To);
        return CommandOutcome.Ok();
    }
}
