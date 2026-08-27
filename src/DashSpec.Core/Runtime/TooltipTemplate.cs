using System.Text;
using System.Text.RegularExpressions;
using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

/// <summary>Interpolates <c>{slot}</c> placeholders against <see cref="TooltipDefinition.Variables"/>.</summary>
public static partial class TooltipTemplate
{
    public static IReadOnlyList<string> CollectSlots(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var slots = new List<string>();
        foreach (var fragment in Parse(template))
        {
            if (fragment.Slot is { } slot &&
                !slots.Contains(slot, StringComparer.OrdinalIgnoreCase))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    public static void Validate(TooltipDefinition tooltip)
    {
        ArgumentNullException.ThrowIfNull(tooltip);

        foreach (var slot in CollectSlots(tooltip.Template))
        {
            if (!tooltip.Variables.ContainsKey(slot))
            {
                throw new InvalidOperationException(
                    $"Tooltip '{tooltip.Id}': placeholder '{{{slot}}}' is not declared in variables.");
            }
        }
    }

    public static IEnumerable<string> SelectColumns(TooltipDefinition tooltip)
    {
        foreach (var slot in CollectSlots(tooltip.Template))
        {
            if (tooltip.Variables.TryGetValue(slot, out var column) &&
                !string.IsNullOrWhiteSpace(column))
            {
                yield return column;
            }
        }
    }

    public static string? Render(
        TooltipDefinition tooltip,
        IReadOnlyDictionary<string, object?> row)
    {
        ArgumentNullException.ThrowIfNull(tooltip);
        ArgumentNullException.ThrowIfNull(row);

        var sb = new StringBuilder();
        foreach (var fragment in Parse(tooltip.Template))
        {
            if (fragment.Literal is { } literal)
            {
                sb.Append(literal);
                continue;
            }

            var slot = fragment.Slot!;
            if (!tooltip.Variables.TryGetValue(slot, out var column))
            {
                throw new InvalidOperationException(
                    $"Tooltip '{tooltip.Id}': placeholder '{{{slot}}}' is not declared in variables.");
            }

            sb.Append(PayloadRowFormatters.FormatHeatmapLabel(row.GetValueOrDefault(column)));
        }

        var text = sb.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static IEnumerable<Fragment> Parse(string template)
    {
        var i = 0;
        while (i < template.Length)
        {
            if (template[i] == '\\' && i + 1 < template.Length)
            {
                var next = template[i + 1];
                yield return next switch
                {
                    'n' => new Fragment(Literal: "\n"),
                    '{' or '}' or '\\' => new Fragment(Literal: next.ToString()),
                    _ => new Fragment(Literal: next.ToString()),
                };
                i += 2;
                continue;
            }

            if (template[i] == '{')
            {
                var close = template.IndexOf('}', i + 1);
                if (close < 0)
                {
                    throw new InvalidOperationException("Tooltip template has an unclosed '{'.");
                }

                var name = template[(i + 1)..close].Trim();
                if (string.IsNullOrWhiteSpace(name) || !SlotIdentRegex().IsMatch(name))
                {
                    throw new InvalidOperationException(
                        $"Tooltip template placeholder '{{{name}}}' is not a valid identifier.");
                }

                yield return new Fragment(Slot: name);
                i = close + 1;
                continue;
            }

            var start = i;
            while (i < template.Length && template[i] is not ('{' or '\\'))
            {
                i++;
            }

            yield return new Fragment(Literal: template[start..i]);
        }
    }

    private readonly record struct Fragment(string? Literal = null, string? Slot = null);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SlotIdentRegex();
}
