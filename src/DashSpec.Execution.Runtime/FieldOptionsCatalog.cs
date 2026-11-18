using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Domain value object for distinct candidate values of field filters (report-scope or card-scope).
/// Eliminates raw <c>IReadOnlyDictionary&lt;string, IReadOnlyList&lt;string&gt;&gt;</c> primitive obsession.
/// </summary>
public sealed class FieldOptionsCatalog : IReadOnlyDictionary<string, IReadOnlyList<string>>
{
    private readonly Dictionary<string, IReadOnlyList<string>> _entries;

    public static FieldOptionsCatalog Empty { get; } = new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));

    public FieldOptionsCatalog()
        : this(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase))
    {
    }

    public FieldOptionsCatalog(IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries is Dictionary<string, IReadOnlyList<string>> dict && Equals(dict.Comparer, StringComparer.OrdinalIgnoreCase)
            ? new Dictionary<string, IReadOnlyList<string>>(dict, StringComparer.OrdinalIgnoreCase)
            : entries.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns candidate options for <paramref name="filterName"/>, or an empty list if none are registered.</summary>
    public IReadOnlyList<string> ForFilter(string filterName) =>
        !string.IsNullOrWhiteSpace(filterName) && _entries.TryGetValue(filterName, out var values)
            ? values
            : [];

    /// <summary>Returns a new catalog with <paramref name="overlay"/> entries overriding or extending this catalog.</summary>
    public FieldOptionsCatalog WithOverlay(FieldOptionsCatalog? overlay)
    {
        if (overlay is null || overlay.Count == 0)
        {
            return this;
        }

        if (_entries.Count == 0)
        {
            return overlay;
        }

        var merged = new Dictionary<string, IReadOnlyList<string>>(_entries, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in overlay._entries)
        {
            merged[k] = v;
        }

        return new FieldOptionsCatalog(merged);
    }

    public IReadOnlyList<string> this[string key] => _entries[key];

    public IEnumerable<string> Keys => _entries.Keys;

    public IEnumerable<IReadOnlyList<string>> Values => _entries.Values;

    public int Count => _entries.Count;

    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out IReadOnlyList<string> value) =>
        _entries.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator() =>
        _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
