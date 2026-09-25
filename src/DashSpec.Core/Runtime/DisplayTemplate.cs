using System.Text;
using System.Text.RegularExpressions;

namespace DashSpec.Core.Runtime;

/// <summary>Interpolates <c>{slot}</c> placeholders in display strings (ADR-0058).</summary>
public static partial class DisplayTemplate
{
    public static bool ContainsSlots(string? template) =>
        !string.IsNullOrEmpty(template) && template.Contains('{', StringComparison.Ordinal);

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

    public static string Render(string template, Func<string, string> resolveSlot)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(resolveSlot);

        var sb = new StringBuilder();
        foreach (var fragment in Parse(template))
        {
            if (fragment.Literal is { } literal)
            {
                sb.Append(literal);
                continue;
            }

            sb.Append(resolveSlot(fragment.Slot!));
        }

        return sb.ToString();
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
                    throw new InvalidOperationException("Display template has an unclosed '{'.");
                }

                var name = template[(i + 1)..close].Trim();
                if (string.IsNullOrWhiteSpace(name) || !SlotIdentRegex().IsMatch(name))
                {
                    throw new InvalidOperationException(
                        $"Display template placeholder '{{{name}}}' is not a valid identifier.");
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
