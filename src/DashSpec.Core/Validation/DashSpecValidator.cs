using DashSpec.Core.Parsing;

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
        _ = DashSpecParser.Parse(File.ReadAllText(path), directory, parseOptions);
    }

    public static void ValidateCatalog(string path) => CatalogParser.ParseFile(path);
}
