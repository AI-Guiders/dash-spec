using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;

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
            return ValidateByExtension(text, filePath, specDirectory, parseOptions);
        }
        catch (DashSpecParseException ex)
        {
            return [TextPositions.ToDiagnostic(text, ex.Message, ex.SourceOffset)];
        }
        catch (FileNotFoundException ex)
        {
            return [MissingFileDiagnostic(text, ex)];
        }
        catch (DirectoryNotFoundException ex)
        {
            return [MissingFileDiagnostic(text, ex)];
        }
        catch (Exception ex)
        {
            return [new DashSpecDiagnostic(0, 0, 0, 1, ex.Message)];
        }
    }

    /// <summary>Missing asset: keep the loader's own message and point at the file name when the text mentions it.</summary>
    private static DashSpecDiagnostic MissingFileDiagnostic(string text, Exception ex)
    {
        var missingPath = ex is FileNotFoundException fnf && !string.IsNullOrWhiteSpace(fnf.FileName)
            ? fnf.FileName
            : null;

        int? offset = null;
        if (!string.IsNullOrWhiteSpace(missingPath))
        {
            var name = Path.GetFileName(missingPath.TrimEnd('"', '\''));
            if (!string.IsNullOrWhiteSpace(name))
            {
                var idx = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    offset = idx;
                }
            }
        }

        return TextPositions.ToDiagnostic(text, ex.Message, offset);
    }

    private static IReadOnlyList<DashSpecDiagnostic> ValidateByExtension(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        switch (Path.GetExtension(filePath).ToLowerInvariant())
        {
            case ".dashcatalog":
                return LintCatalog(text, filePath, specDirectory, parseOptions);
            case ".dashdiagram":
                DiagramModuleParser.ParseDiagramFile(text, specDirectory);
                return [];
            case ".dashpresentation":
                PresentationModuleParser.ParsePresentationFile(text, specDirectory);
                return [];
            case ".dashpalette":
                PaletteModuleParser.ParsePaletteFile(text);
                return [];
            case ".dashtransform":
                TransformModuleParser.ParseTransformFile(text);
                return [];
            case ".dashlayout":
                LayoutModuleParser.ParseLayoutFile(text);
                return [];
            default:
                var document = DashSpecParser.Parse(text, specDirectory, parseOptions);
                return ToDiagnostics(ResolutionLint.Analyze(document));
        }
    }

    private static IReadOnlyList<DashSpecDiagnostic> LintCatalog(
        string text,
        string catalogPath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        var catalog = CatalogParser.Parse(text);
        var findings = new List<ResolutionLintFinding>();
        foreach (var entry in catalog.Entries)
        {
            var specPath = CatalogParser.ResolveEntrySpecPath(catalogPath, entry.DashspecPath);
            var specDirectoryForEntry = Path.GetDirectoryName(specPath)!;
            var document = DashSpecParser.Parse(File.ReadAllText(specPath), specDirectoryForEntry, parseOptions);
            findings.AddRange(ResolutionLint.Analyze(document, entry));
        }

        return ToDiagnostics(findings);
    }

    private static IReadOnlyList<DashSpecDiagnostic> ToDiagnostics(IReadOnlyList<ResolutionLintFinding> findings)
    {
        if (findings.Count == 0)
        {
            return [];
        }

        return findings
            .Select(finding => new DashSpecDiagnostic(
                0,
                0,
                0,
                1,
                finding.FormatMessage(),
                finding.Severity == ResolutionLintSeverity.Information
                    ? DashSpecDiagnosticSeverity.Information
                    : DashSpecDiagnosticSeverity.Warning))
            .ToList();
    }
}
