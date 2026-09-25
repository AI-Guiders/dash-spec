using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;

namespace DashSpec.Core.Validation;

/// <summary>CLI-friendly validation entry points (ADR-0030).</summary>
public static class DashSpecValidator
{
    public static void ValidateSpec(string path, string? specDirectory = null) =>
        ValidateSpec(path, specDirectory, DashSpecParseOptions.Default);

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

        var directory = specDirectory ?? Path.GetDirectoryName(path)!;
        var document = DashSpecParser.Parse(File.ReadAllText(path), directory, parseOptions);
        EmitResolutionWarnings(ResolutionLint.Analyze(document));
    }

    public static void ValidateCatalog(string path)
    {
        var catalog = CatalogParser.ParseFile(path);
        foreach (var entry in catalog.Entries)
        {
            var specPath = CatalogParser.ResolveEntrySpecPath(path, entry.DashspecPath);
            var specDirectory = Path.GetDirectoryName(specPath)!;
            var document = DashSpecParser.Parse(File.ReadAllText(specPath), specDirectory);
            EmitResolutionWarnings(ResolutionLint.Analyze(document, entry));
        }
    }

    public static IReadOnlyList<ResolutionLintFinding> GetResolutionFindings(
        DashboardDocument document,
        CatalogEntryDefinition? catalogEntry = null) =>
        ResolutionLint.Analyze(document, catalogEntry);

    private static void EmitResolutionWarnings(IReadOnlyList<ResolutionLintFinding> findings)
    {
        foreach (var finding in findings.Where(f => f.Severity == ResolutionLintSeverity.Warning))
        {
            Console.Error.WriteLine($"warning: {finding.FormatMessage()}");
        }
    }
}
