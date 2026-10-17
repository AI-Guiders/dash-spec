using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Reflection;

namespace DashSpec.Surface.Blazor.Services.Presentation;

public static class CardSelectionPresenter
{
    public static IReadOnlyList<string> BuildListItems(
        ShowSelectionEffect effect,
        HeatmapCellContext context,
        MatrixPresentation? presentation) =>
        BuildListParts(effect, context, presentation).Items;

    public static (string? Headline, IReadOnlyList<string> Items) BuildListParts(
        ShowSelectionEffect effect,
        HeatmapCellContext context,
        MatrixPresentation? presentation)
    {
        if (effect.Source is ShowSource.Tooltip)
        {
            var split = effect.Split ?? presentation?.TooltipSplit ?? ", ";
            var raw = context.TooltipRaw;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                string? peakTime = null;
                var body = raw;
                var newline = raw.IndexOf('\n');
                if (newline >= 0)
                {
                    peakTime = raw[..newline].Trim();
                    body = raw[(newline + 1)..];
                }

                var headline = BuildSelectionHeadline(context, peakTime);
                return (headline, SplitTooltip(body, split));
            }

            // Warm heatmap stubs may leave tooltip NULL while the cell still has a value.
            if (context.Value is not null)
            {
                return (
                    BuildSelectionHeadline(context, peakTime: null),
                    BuildCellFallbackItems(context, presentation));
            }

            return (null, []);
        }

        if (effect.Source is ShowSource.Cell)
        {
            return (
                BuildSelectionHeadline(context, peakTime: null),
                BuildCellFallbackItems(context, presentation));
        }

        return (null, []);
    }

    private static IReadOnlyList<string> BuildCellFallbackItems(
        HeatmapCellContext context,
        MatrixPresentation? presentation)
    {
        var rows = BuildKeyValueRows(context, presentation);
        if (rows.Count == 0)
        {
            return [];
        }

        return rows.Select(row => $"{row.Key}: {row.Value}").ToList();
    }

    public static string BuildPlainText(
        ShowSelectionEffect effect,
        HeatmapCellContext context,
        MatrixPresentation? presentation)
    {
        return effect.Source switch
        {
            ShowSource.Tooltip => string.IsNullOrWhiteSpace(context.TooltipRaw)
                ? FormatCellSummary(context, presentation)
                : context.TooltipRaw,
            ShowSource.Cell => string.IsNullOrWhiteSpace(context.TooltipRaw)
                ? FormatCellSummary(context, presentation)
                : context.TooltipRaw,
            _ => string.Empty,
        };
    }

    public static IReadOnlyList<(string Key, string Value)> BuildKeyValueRows(
        HeatmapCellContext context,
        MatrixPresentation? presentation)
    {
        var rows = new List<(string Key, string Value)>();
        if (!string.IsNullOrWhiteSpace(presentation?.XLabel))
        {
            rows.Add((presentation.XLabel, context.XLabel));
        }

        if (!string.IsNullOrWhiteSpace(presentation?.YLabel))
        {
            rows.Add((presentation.YLabel, context.YLabel));
        }

        if (!string.IsNullOrWhiteSpace(presentation?.ValueLabel))
        {
            rows.Add((presentation.ValueLabel, FormatValue(context.Value)));
        }
        else if (context.Value is not null)
        {
            rows.Add(("Value", FormatValue(context.Value)));
        }

        if (!string.IsNullOrWhiteSpace(context.TooltipRaw))
        {
            var tipLabel = presentation?.TooltipLabel ?? "Details";
            rows.Add((tipLabel, context.TooltipRaw));
        }

        return rows;
    }

    public static ShowSelectionEffect? FindShowEffect(DashSpec.Modeling.Parse.Card.CardClickBehaviour? behaviour)
    {
        if (behaviour is null)
        {
            return null;
        }

        foreach (var effect in behaviour.Effects)
        {
            var (caseInfo, fields) = FSharpValue.GetUnionFields(
                effect,
                typeof(DashSpec.Modeling.Parse.Card.CardClickEffect),
                FSharpOption<System.Reflection.BindingFlags>.None);
            if (!string.Equals(caseInfo.Name, "ShowSelection", StringComparison.Ordinal))
            {
                continue;
            }

            var split = (FSharpOption<string>)fields[4]!;
            return new ShowSelectionEffect(
                Enum.Parse<ShowPlacement>(fields[0]!.ToString()!),
                Enum.Parse<ShowFormat>(fields[1]!.ToString()!),
                Enum.Parse<ShowSource>(fields[2]!.ToString()!),
                (bool)fields[3]!,
                split?.Value);
        }

        return null;
    }

    private static string? BuildSelectionHeadline(HeatmapCellContext context, string? peakTime)
    {
        var parts = new List<string> { context.YLabel, context.XLabel };
        if (context.Value is not null)
        {
            parts.Add($"{FormatValue(context.Value)} однов.");
        }

        var headline = string.Join(" · ", parts);
        if (!string.IsNullOrWhiteSpace(peakTime))
        {
            headline += $" · пик в {peakTime}";
        }

        return headline;
    }

    private static string FormatCellSummary(HeatmapCellContext context, MatrixPresentation? presentation)
    {
        var rows = BuildKeyValueRows(context, presentation);
        return string.Join(
            Environment.NewLine,
            rows.Select(row => $"{row.Key}: {row.Value}"));
    }

    private static IReadOnlyList<string> SplitTooltip(string? raw, string split)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string FormatValue(double? value) =>
        value is null ? "—" : value.Value.ToString("0");
}
