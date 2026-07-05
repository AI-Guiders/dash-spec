using System.Globalization;
using DashSpec.Abstractions.Plugins;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Models;
using Microsoft.JSInterop;

namespace DashSpec.Host.Plugins;

public sealed class DashSpecActionDispatcher
{
    private readonly IReadOnlyDictionary<string, IDashSpecActionHandler> _handlers;
    private readonly IJSRuntime _js;

    public DashSpecActionDispatcher(IEnumerable<IDashSpecActionHandler> handlers, IJSRuntime js)
    {
        _handlers = handlers.ToDictionary(x => x.ActionId, StringComparer.OrdinalIgnoreCase);
        _js = js;
    }

    public async Task<DashSpecActionOutcome> ExecuteAsync(
        string actionId,
        CardRenderResult card,
        IReadOnlyDictionary<string, string> args,
        HeatmapCellContext? clickContext = null,
        CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(actionId, out var handler))
        {
            return new DashSpecActionOutcome();
        }

        var mergedArgs = new Dictionary<string, string>(args, StringComparer.OrdinalIgnoreCase);
        if (clickContext is not null)
        {
            mergedArgs.TryAdd("x", clickContext.XLabel);
            mergedArgs.TryAdd("y", clickContext.YLabel);
            if (clickContext.Value is not null)
            {
                mergedArgs.TryAdd("value", clickContext.Value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        var context = BuildContext(card);
        var outcome = await handler.ExecuteAsync(context, mergedArgs, cancellationToken).ConfigureAwait(false);
        if (outcome.Kind is DashSpecActionOutcomeKind.DownloadText &&
            !string.IsNullOrWhiteSpace(outcome.TextContent))
        {
            await _js.InvokeVoidAsync(
                "dashspecDownload.downloadText",
                cancellationToken,
                outcome.FileName ?? "export.csv",
                outcome.TextContent,
                outcome.MimeType ?? "text/csv;charset=utf-8").ConfigureAwait(false);
        }

        return outcome;
    }

    internal static DashSpecActionContext BuildContext(CardRenderResult card)
    {
        if (card.Table is { Columns.Count: > 0 } table)
        {
            return new DashSpecActionContext(
                card.Id,
                card.Title,
                new DashSpecTableData(table.Columns, table.Rows));
        }

        if (card.Matrix is { } matrix)
        {
            var rows = new List<IReadOnlyList<string>>();
            for (var yi = 0; yi < matrix.YLabels.Count; yi++)
            {
                for (var xi = 0; xi < matrix.XLabels.Count; xi++)
                {
                    var value = matrix.Cells[yi][xi];
                    rows.Add([
                        matrix.XLabels[xi],
                        matrix.YLabels[yi],
                        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    ]);
                }
            }

            return new DashSpecActionContext(
                card.Id,
                card.Title,
                new DashSpecTableData(["x", "y", "value"], rows));
        }

        if (card.Chart is { Labels.Count: > 0 } chart)
        {
            var columns = new List<string> { "category" };
            columns.AddRange(chart.Series.Select(series => series.Name));

            var rows = new List<IReadOnlyList<string>>();
            for (var i = 0; i < chart.Labels.Count; i++)
            {
                var row = new List<string> { chart.Labels[i] };
                foreach (var series in chart.Series)
                {
                    var value = i < series.Values.Count ? series.Values[i] : null;
                    row.Add(value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                }

                rows.Add(row);
            }

            return new DashSpecActionContext(
                card.Id,
                card.Title,
                new DashSpecTableData(columns, rows));
        }

        return new DashSpecActionContext(card.Id, card.Title, null);
    }
}
