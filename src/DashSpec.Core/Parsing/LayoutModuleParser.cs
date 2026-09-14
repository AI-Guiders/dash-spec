using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Transitional entry — delegates to F# Modeling.Parse via <see cref="LayoutParseBridge"/>.</summary>
public static class LayoutModuleParser
{
    public static LayoutBoardDefinition ParseLayoutFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (LayoutParseBridge.ParseLayoutFile is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException(
            "Layout parse bridge not registered. Reference DashSpec.Execution.Core or register LayoutParseBridge.ParseLayoutFile.");
    }

    public static LayoutBoardDefinition Load(string reference, string specDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);

        var path = SpecIncludeResolver.ResolvePath(reference, specDirectory);
        path = ResolveLayoutFile(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Include layout not found: '{reference}' (resolved: {path}).",
                path);
        }

        return ParseLayoutFile(File.ReadAllText(path));
    }

    private static string ResolveLayoutFile(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        const string extension = ".dashlayout";
        var withExt = path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;
        return File.Exists(withExt) ? withExt : path;
    }
}