using System.Collections.Concurrent;

namespace DashSpec.Abstractions.Query;

/// <summary>Registry of <see cref="ISqlDialectBackend"/> implementations (built-ins + connector plugins).</summary>
public static class SqlDialectRegistry
{
    private static readonly ConcurrentDictionary<string, ISqlDialectBackend> Backends =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<ISqlDialectBackend> All =>
        Backends.Values.ToArray();

    public static void Register(ISqlDialectBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        if (string.IsNullOrWhiteSpace(backend.Id))
        {
            throw new ArgumentException("Dialect backend id is required.", nameof(backend));
        }

        if (!Backends.TryAdd(backend.Id, backend))
        {
            throw new InvalidOperationException(
                $"SQL dialect backend '{backend.Id}' is already registered.");
        }
    }

    public static ISqlDialectBackend Resolve(string id)
    {
        if (TryResolve(id, out var backend))
        {
            return backend;
        }

        throw new InvalidOperationException(
            $"Unknown SQL dialect '{id}'. Registered: {string.Join(", ", Backends.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}.");
    }

    public static bool TryResolve(string id, out ISqlDialectBackend backend)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            backend = null!;
            return false;
        }

        return Backends.TryGetValue(id.Trim(), out backend!);
    }
}
