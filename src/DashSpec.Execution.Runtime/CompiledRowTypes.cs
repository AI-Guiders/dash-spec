using System.Reflection;
using System.Runtime.Loader;
using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// ADR-0124: the compiled row types (TypedRows.dll built by <c>dsc build types</c>) loaded
/// next to the catalog. One assembly load at host bootstrap; afterwards the hot path is a
/// dictionary lookup and a compiled delegate — no reflection per call. Absent assembly =
/// the wire mode (DashValue[] rows), everything works as before.
/// </summary>
public static class CompiledRowTypes
{
    private static readonly Dictionary<string, Func<DashValue[], string[], object>> Factories =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Type> Types = new(StringComparer.OrdinalIgnoreCase);

    private static AssemblyLoadContext? _context;

    public static string? LoadedFrom { get; private set; }

    /// <summary>Loads a TypedRows.dll (idempotent per path). The generated RowTypeRegistry's
    /// Create is bound to a compiled delegate once; the load context stays alive for the host's
    /// lifetime.</summary>
    public static void Load(string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        var fullPath = Path.GetFullPath(assemblyPath);
        if (string.Equals(LoadedFrom, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var context = new AssemblyLoadContext("DashSpec.TypedRows", isCollectible: false);
        var assembly = context.LoadFromAssemblyPath(fullPath);
        var registry = assembly.GetType("DashSpec.TypedRows.RowTypeRegistry")
            ?? throw new InvalidOperationException(
                $"{fullPath}: RowTypeRegistry not found — is this a DashSpec.TypedRows assembly?");

        var create = registry.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{fullPath}: RowTypeRegistry.Create not found.");

        var factory = create.CreateDelegate<Func<string, DashValue[], string[], object>>();

        foreach (var typeName in ListTypeNames(registry))
        {
            if (Factories.TryGetValue(typeName, out _))
            {
                continue;
            }

            // Bind a per-name delegate upfront — the name check happens here, once.
            Factories[typeName] = (cells, names) => factory(typeName, cells, names);
            Types[typeName] = assembly.GetType($"DashSpec.TypedRows.{typeName}")
                ?? typeof(object);
        }

        _context = context;
        LoadedFrom = fullPath;
    }

    public static bool Contains(string rowTypeName) => Factories.ContainsKey(rowTypeName);

    /// <summary>The compiled CLR type of a row type (for generic component instantiation).</summary>
    public static Type TypeOf(string rowTypeName) =>
        Types.TryGetValue(rowTypeName, out var type) ? type : throw new InvalidOperationException($"Row type '{rowTypeName}' is not compiled into TypedRows.");

    public static object Create(string rowTypeName, DashValue[] cells, string[] fieldNames) =>
        Factories.TryGetValue(rowTypeName, out var factory)
            ? factory(cells, fieldNames)
            : throw new InvalidOperationException($"Row type '{rowTypeName}' is not compiled into TypedRows.");



    private static IEnumerable<string> ListTypeNames(Type registry)
    {
        // The registry closes over the POCOs directly; the type names ride the factory keys —
        // surfaced via Contains probes is not viable, so read the generated map through the
        // registry's own static field (single reflective read at load time).
        var field = registry.GetField("Factories", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.GetValue(null) is System.Collections.IDictionary map)
        {
            foreach (var key in map.Keys)
            {
                if (key is string name)
                {
                    yield return name;
                }
            }
        }
    }
}
