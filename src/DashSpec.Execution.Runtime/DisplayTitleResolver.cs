using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

/// <summary>Resolves display titles with <c>bind display</c> slots (ADR-0058).</summary>
public static class DisplayTitleResolver
{
    public static string? Resolve(
        string? template,
        IReadOnlyDictionary<string, string>? bindings,
        FilterDisplayContext context)
    {
        if (string.IsNullOrWhiteSpace(template) || !DisplayTemplate.ContainsSlots(template))
        {
            return template;
        }

        return DisplayTemplate.Render(template, slot => ResolveSlot(slot, bindings, context));
    }

    private static string ResolveSlot(
        string slot,
        IReadOnlyDictionary<string, string>? bindings,
        FilterDisplayContext context)
    {
        if (bindings is not null &&
            bindings.TryGetValue(slot, out var source) &&
            !string.IsNullOrWhiteSpace(source))
        {
            return FilterDisplaySource.Resolve(source, context);
        }

        if (context.FilterIndex.ContainsKey(slot))
        {
            return FilterDisplaySource.Resolve(slot, context);
        }

        return string.Empty;
    }
}
