using DashSpec.Core.Parsing;

namespace DashSpec.Core.Validation;

public static class DashSpecDiagnosticService
{
    public static IReadOnlyList<DashSpecDiagnostic> ValidateFile(
        string path,
        string? specDirectory = null,
        DashSpecParseOptions? parseOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        parseOptions ??= DashSpecParseOptions.Editor;
        if (!File.Exists(path))
        {
            return
            [
                new DashSpecDiagnostic(0, 0, 0, 1, $"File not found: {path}"),
            ];
        }

        var text = File.ReadAllText(path);
        var directory = specDirectory ?? Path.GetDirectoryName(path)!;
        return ValidateText(text, path, directory, parseOptions);
    }

    public static IReadOnlyList<DashSpecDiagnostic> ValidateText(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions? parseOptions = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);
        parseOptions ??= DashSpecParseOptions.Editor;

        try
        {
            ValidateByExtension(text, filePath, specDirectory, parseOptions);
            return Array.Empty<DashSpecDiagnostic>();
        }
        catch (DashSpecParseException ex)
        {
            return [TextPositions.ToDiagnostic(text, ex.Message, ex.SourceOffset)];
        }
        catch (FileNotFoundException ex)
        {
            return MapIncludeNotFoundDiagnostic(text, specDirectory, ex);
        }
        catch (Exception ex) when (ex.Message.StartsWith("!include not found:", StringComparison.Ordinal))
        {
            return MapIncludeNotFoundDiagnostic(text, specDirectory, ex);
        }
        catch (Exception ex)
        {
            return [new DashSpecDiagnostic(0, 0, 0, 1, ex.Message)];
        }
    }

    private static IReadOnlyList<DashSpecDiagnostic> MapIncludeNotFoundDiagnostic(
        string text,
        string specDirectory,
        Exception ex)
    {
        var missingPath = ex is FileNotFoundException fnf && !string.IsNullOrWhiteSpace(fnf.FileName)
            ? fnf.FileName
            : ExtractIncludePathFromMessage(ex.Message);

        var offset = !string.IsNullOrWhiteSpace(missingPath)
            ? IncludeReferenceHeuristics.TryFindIncludeOffset(text, missingPath, specDirectory)
            : null;

        return [TextPositions.ToDiagnostic(text, ex.Message, offset)];
    }

    private static string? ExtractIncludePathFromMessage(string message)
    {
        var start = message.IndexOf('\'');
        if (start < 0)
        {
            return null;
        }

        var end = message.IndexOf('\'', start + 1);
        return end > start ? message[(start + 1)..end] : null;
    }

    private static void ValidateByExtension(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        switch (Path.GetExtension(filePath).ToLowerInvariant())
        {
            case ".dashcatalog":
                CatalogParser.Parse(text);
                break;
            case ".dashdiagram":
                DiagramModuleParser.ParseDiagramFile(text, specDirectory);
                break;
            case ".dashpresentation":
                PresentationModuleParser.ParsePresentationFile(text, specDirectory);
                break;
            case ".dashpalette":
                PaletteModuleParser.ParsePaletteFile(text);
                break;
            case ".dashtransform":
                TransformModuleParser.ParseTransformFile(text);
                break;
            case ".dashlayout":
                LayoutModuleParser.ParseLayoutFile(text);
                break;
            default:
                _ = DashSpecParser.Parse(text, specDirectory, parseOptions);
                break;
        }
    }
}
