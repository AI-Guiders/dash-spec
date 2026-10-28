using System.Text.RegularExpressions;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Drill slot title (ADR-0107 drill UX): fills <c>{filter}</c> placeholders of the table diagram
/// <c>title = …</c> template from the cell drill overlay. An unfillable placeholder returns null —
/// the title waits for its click.
/// </summary>
public static class DrillSlotTitleResolver
{
    private static readonly Regex PlaceholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>Resolved title text, or null when the template is empty or a placeholder has no overlay value.</summary>
    public static string? Resolve(string? template, CardCellDrillOverlay? overlay)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var unfillable = false;
        var filled = PlaceholderPattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (overlay is not null)
            {
                if (overlay.TryGetDate(name, out var range))
                {
                    return LabelFormat.FormatObject(range.From, LabelFormat.ResolveDateFormat("date.short"));
                }

                // Заголовок — для человека: display-кадр (метка оси), не storage-значение запроса.
                if (overlay.TryGetFieldDisplay(name, out var display) &&
                    !string.IsNullOrWhiteSpace(display))
                {
                    return display;
                }

                if (overlay.TryGetField(name, out var values))
                {
                    var value = values.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            unfillable = true;
            return match.Value;
        });

        return unfillable ? null : filled;
    }
}
