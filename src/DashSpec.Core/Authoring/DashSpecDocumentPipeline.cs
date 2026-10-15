namespace DashSpec.Core.Authoring;

public static class DashSpecDocumentPipeline
{
    static readonly HashSet<string> DashSpecDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Units and shells (ADR-0101/0102). `.sql` and `.toml` are repo assets that still get newline hygiene.
        ".ds", ".sql", ".toml",
    };

    public static EditorConfig.EditorConfigOptions ResolveOptions(string absoluteFilePath, string? searchRoot = null) =>
        EditorConfig.EditorConfigResolver.ResolveForFile(absoluteFilePath, searchRoot);

    public static bool SupportsDocumentPipeline(string absoluteFilePath) =>
        DashSpecDocumentExtensions.Contains(Path.GetExtension(absoluteFilePath));

    public static string Format(string text, string absoluteFilePath, string? searchRoot = null)
    {
        var options = ResolveOptions(absoluteFilePath, searchRoot);
        var normalized = Formatting.DashSpecTextHygiene.NormalizeNewlines(text);
        var formatted = IsDashSpecSurface(absoluteFilePath)
            ? FormatDashSpec(normalized, options)
            : normalized;
        return Formatting.DashSpecTextHygiene.Apply(formatted, options);
    }

    public static string PrepareForSave(string text, string absoluteFilePath, string? searchRoot = null)
    {
        if (!SupportsDocumentPipeline(absoluteFilePath))
        {
            return text;
        }

        var options = ResolveOptions(absoluteFilePath, searchRoot);
        var working = options.DashSpecFormatOnSave
            ? Format(text, absoluteFilePath, searchRoot)
            : Formatting.DashSpecTextHygiene.Apply(text, options);
        return working;
    }

    static string FormatDashSpec(string text, EditorConfig.EditorConfigOptions options)
    {
        if (DocumentFormatBridge.FormatDashSpec is { } format)
        {
            return format(text, options);
        }

        throw new InvalidOperationException(
            "DashSpec format bridge not registered. Reference DashSpec.Execution.Core or call ModuleParseRegistration.EnsureRegistered().");
    }

    static bool IsDashSpecSurface(string absoluteFilePath)
    {
        var ext = Path.GetExtension(absoluteFilePath);
        return ext.Equals(".ds", StringComparison.OrdinalIgnoreCase) ||
               ext.StartsWith(".dash", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".catalog.gdl", StringComparison.OrdinalIgnoreCase);
    }
}
