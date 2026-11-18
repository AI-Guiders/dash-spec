using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Domain relation mapping each filter name to the ordered list of card ids bound to it.
/// Eliminates raw <c>IReadOnlyDictionary&lt;string, IReadOnlyList&lt;string&gt;&gt;</c> primitive obsession.
/// </summary>
public sealed class FilterCardBindings : IReadOnlyDictionary<string, IReadOnlyList<string>>
{
    private readonly Dictionary<string, IReadOnlyList<string>> _byFilter;

    public static FilterCardBindings Empty { get; } = new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));

    public FilterCardBindings()
        : this(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public FilterCardBindings(IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> byFilter)
    {
        ArgumentNullException.ThrowIfNull(byFilter);
        _byFilter = byFilter is Dictionary<string, IReadOnlyList<string>> dict && Equals(dict.Comparer, StringComparer.OrdinalIgnoreCase)
            ? new Dictionary<string, IReadOnlyList<string>>(dict, StringComparer.OrdinalIgnoreCase)
            : byFilter.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Cards bound to <paramref name="filterName"/> (empty when unbound).</summary>
    public IReadOnlyList<string> CardsForFilter(string filterName) =>
        !string.IsNullOrWhiteSpace(filterName) && _byFilter.TryGetValue(filterName, out var cards)
            ? cards
            : [];

    /// <summary>True if <paramref name="filterName"/> is bound to at least one card in <paramref name="activeCardIds"/>.</summary>
    public bool IsBoundToAnyCard(string filterName, IReadOnlySet<string> activeCardIds) =>
        CardsForFilter(filterName).Any(activeCardIds.Contains);

    public IReadOnlyList<string> this[string key] => _byFilter[key];

    public IEnumerable<string> Keys => _byFilter.Keys;

    public IEnumerable<IReadOnlyList<string>> Values => _byFilter.Values;

    public int Count => _byFilter.Count;

    public bool ContainsKey(string key) => _byFilter.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out IReadOnlyList<string> value) =>
        _byFilter.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator() =>
        _byFilter.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
