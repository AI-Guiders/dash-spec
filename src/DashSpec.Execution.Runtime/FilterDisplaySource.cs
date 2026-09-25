using DashSpec.Core.Model;
using DashSpec.Core.Resolution;

namespace DashSpec.Execution.Runtime;

/// <summary>Resolves <c>filter.property</c> display binding sources (ADR-0058).</summary>
public static class FilterDisplaySource
{
    public static string Resolve(string source, FilterDisplayContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(context);

        var parts = source.Split('.', 2, StringSplitOptions.TrimEntries);
        var filterName = parts[0];
        var property = parts.Length > 1 ? parts[1] : null;

        if (!context.FilterIndex.TryGetValue(filterName, out var filter))
        {
            return string.Empty;
        }

        property ??= DefaultProperty(filter.Kind);

        return filter.Kind switch
        {
            FilterKind.Top => ResolveTop(filter, property, context),
            FilterKind.Date => ResolveDate(filter, property, context),
            FilterKind.Field => ResolveField(filter, property, context),
            _ => string.Empty,
        };
    }

    private static string DefaultProperty(FilterKind kind) =>
        kind switch
        {
            FilterKind.Top => "value",
            FilterKind.Date => "range",
            FilterKind.Field => "chip",
            _ => "value",
        };

    private static string ResolveTop(
        FilterDefinition filter,
        string property,
        FilterDisplayContext context)
    {
        return property.ToLowerInvariant() switch
        {
            "value" => FormatTopValue(
                filter,
                context.TopLimits.TryGetValue(filter.Name, out var current) ? current : null),
            "label" => DisplayResolution.ResolveFilterLabel(filter),
            _ => string.Empty,
        };
    }

    private static string FormatTopValue(FilterDefinition filter, int? current)
    {
        var value = TopLimitDefaults.Resolve(filter, current);
        if (filter.MinValue == 0 && value == 0)
        {
            return "все";
        }

        return value.ToString();
    }

    private static string ResolveDate(
        FilterDefinition filter,
        string property,
        FilterDisplayContext context)
    {
        if (!context.DateFrom.TryGetValue(filter.Name, out var from) ||
            !context.DateTo.TryGetValue(filter.Name, out var to))
        {
            return string.Empty;
        }

        var grain = GrainFilterPresentation.ResolveGrain(
            filter,
            context.FilterIndex,
            context.SelectedFields);

        return property.ToLowerInvariant() switch
        {
            "range" => GrainFilterPresentation.FormatChipValue(from, to, grain),
            "value" => GrainFilterPresentation.FormatChipValue(from, from, grain),
            "from" => LabelFormat.FormatObject(from, LabelFormat.ResolveDateFormat(null)),
            "to" => LabelFormat.FormatObject(to, LabelFormat.ResolveDateFormat(null)),
            "grain" => grain ?? string.Empty,
            "label" => GrainFilterPresentation.DisplayLabel(
                filter,
                context.FilterIndex,
                context.SelectedFields),
            _ => string.Empty,
        };
    }

    private static string ResolveField(
        FilterDefinition filter,
        string property,
        FilterDisplayContext context)
    {
        return property.ToLowerInvariant() switch
        {
            "chip" => FormatFieldChip(filter, context),
            "label" => DisplayResolution.ResolveFilterLabel(filter),
            "value" => FormatFieldValue(filter, context),
            _ => string.Empty,
        };
    }

    private static string FormatFieldChip(FilterDefinition filter, FilterDisplayContext context)
    {
        if (!context.SelectedFields.TryGetValue(filter.Name, out var selected) || selected.Count == 0)
        {
            return "все";
        }

        var values = selected.Take(3).ToList();
        var suffix = selected.Count > 3 ? $" +{selected.Count - 3}" : string.Empty;
        return $"{string.Join(", ", values)}{suffix}";
    }

    private static string FormatFieldValue(FilterDefinition filter, FilterDisplayContext context)
    {
        if (!context.SelectedFields.TryGetValue(filter.Name, out var selected) || selected.Count == 0)
        {
            return string.Empty;
        }

        return selected.First();
    }
}
