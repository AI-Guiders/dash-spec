using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;


using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Compilation;

/// <summary>
/// CLI-friendly validation entry points (ADR-0030). One engine: the per-file diagnostic
/// service over the assembled corpus — there is no standalone-parse second path anymore;
/// legacy flat document formats are retired (ADR-0101 single unit file type).
/// </summary>
public static class DashSpecValidator
{
    public static void ValidateSpec(
        string path,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(parseOptions);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec file not found.", path);
        }

        if (!Path.GetExtension(path).Equals(".ds", StringComparison.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Not a .ds unit: '{path}'. Single-unit files are the only format (ADR-0101); legacy document formats are retired.");
        }

        var directory = specDirectory ?? Path.GetDirectoryName(path)!;
        ThrowOnDiagnostics(DashSpecDiagnosticService.ValidateFile(path, directory, parseOptions));
    }

    /// <summary>
    /// Catalog validation rides the per-file diagnostic service: dashboard entries validate
    /// the way the runtime assembles them (imports merged, root filters visible to section
    /// cards); sections route by their own unit kind.
    /// </summary>
    public static void ValidateCatalog(string path, DashSpecParseOptions? parseOptions = null) =>
        ThrowOnDiagnostics(DashSpecDiagnosticService.ValidateFile(path, parseOptions: parseOptions));

    private static void ThrowOnDiagnostics(IReadOnlyList<DashSpecDiagnostic> diagnostics)
    {
        var error = diagnostics.FirstOrDefault(d => d.Severity == DashSpecDiagnosticSeverity.Error);
        if (error is not null)
        {
            throw new DashSpecParseException(error.Message);
        }

        foreach (var warning in diagnostics.Where(d => d.Severity == DashSpecDiagnosticSeverity.Warning))
        {
            Console.Error.WriteLine($"warning: {warning.Message}");
        }
    }
}
