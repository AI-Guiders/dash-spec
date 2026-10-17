using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using FsharpDocument = DashSpec.Modeling.Parse.Document;

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
            return ValidateByFileKind(text, filePath, specDirectory, parseOptions);
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

    /// <summary>
    /// A `.ds` file is routed by the kind in its module header (ADR-0101); anything else is treated as a document.
    /// </summary>
    private static IReadOnlyList<DashSpecDiagnostic> ValidateByFileKind(
        string text,
        string filePath,
        string specDirectory,
        DashSpecParseOptions parseOptions)
    {
        if (!Path.GetExtension(filePath).Equals(".ds", StringComparison.OrdinalIgnoreCase))
        {
            var document = DashSpecParser.Parse(text, specDirectory, parseOptions);
            return ToDiagnostics(ResolutionLint.Analyze(document));
        }

        var (unitKind, _) = FsharpDocument.UnitSource.tryKindAndId(text);

        switch (unitKind.ToLowerInvariant())
        {
            case "diagram":
                DiagramModuleParser.ParseDiagramFile(text, specDirectory);
                return [];
            case "presentation":
                PresentationModuleParser.ParsePresentationFile(text, specDirectory);
                return [];
            case "palette":
                PaletteModuleParser.ParsePaletteFile(text);
                return [];
            case "transform":
                TransformModuleParser.ParseTransformFile(text);
                return [];
            case "layout":
                LayoutModuleParser.ParseLayoutFile(text);
                return [];
            case "tooltip":
                TooltipModuleParser.ParseTooltipFile(text);
                return [];
            case "types":
            case "flow":
                // Type and flow units have no standalone validation facade: they are validated in the
                // context of the module that imports them.
                return [];
            case "catalog":
                return LintCatalog(text, filePath, specDirectory, parseOptions);
            case "host":
                HostModuleParser.Parse(text, specDirectory);
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
        var catalog = CatalogReader.Parse(text);
        var findings = new List<ResolutionLintFinding>();
        foreach (var entry in catalog.Entries)
        {
            var specPath = CatalogReader.ResolveEntrySpecPath(catalogPath, entry.DashspecPath);
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
