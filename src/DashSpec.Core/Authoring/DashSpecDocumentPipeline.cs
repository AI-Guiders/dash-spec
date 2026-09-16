namespace DashSpec.Core.Authoring;

public static class DashSpecDocumentPipeline
{
    static readonly HashSet<string> DashSpecDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dashspec", ".dashlibrary", ".dashlayout", ".dashdiagram", ".dashpalette",
        ".dashcatalog", ".dashtooltip", ".dashpresentation", ".sql", ".toml",
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
            ? Formatting.DashSpecTextFormatter.Format(normalized, options)
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

    static bool IsDashSpecSurface(string absoluteFilePath)
    {
        var ext = Path.GetExtension(absoluteFilePath);
        return ext.StartsWith(".dash", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".catalog.gdl", StringComparison.OrdinalIgnoreCase);
    }
}
