using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Resolved diagram / presentation / transform / tooltip fragment from an include file.</summary>
internal sealed record SpecIncludeFragment(
    DiagramDefinition? Diagram,
    PresentationBlock? Presentation,
    SeriesTransformBlock? SeriesTransform,
    IReadOnlyDictionary<string, TooltipDefinition>? Tooltips = null,
    InspectPresentation? Inspect = null);

internal static class SpecIncludeResolver
{
    private static string? _stdlibRootOverride;

    internal static void SetStdlibRootForTests(string? path) => _stdlibRootOverride = path;

    public static string ResolvePath(string reference, string specDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);

        if (IsStdlibReference(reference))
        {
            var inner = reference[1..^1].Trim().Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(GetStdlibRoot(), inner);
        }

        var combined = Path.IsPathRooted(reference)
            ? reference
            : Path.GetFullPath(Path.Combine(specDirectory, reference));

        return combined;
    }

    public static SpecIncludeFragment Load(string includeKind, string reference, string specDirectory)
    {
        var path = ResolvePath(reference, specDirectory);
        path = ResolveExistingFile(path, includeKind);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Include {includeKind} not found: '{reference}' (resolved: {path}).",
                path);
        }

        var text = File.ReadAllText(path);
        var baseDirectory = Path.GetDirectoryName(path) ?? specDirectory;

        return includeKind.ToLowerInvariant() switch
        {
            "diagram" => DiagramModuleParser.ParseDiagramFile(text, baseDirectory),
            "presentation" or "chrome" => new SpecIncludeFragment(
                null,
                PresentationModuleParser.ParsePresentationFile(text, baseDirectory),
                null),
            "transform" => new SpecIncludeFragment(null, null, TransformModuleParser.ParseTransformFile(text)),
            "tooltip" => LoadTooltipFragment(text),
            _ => throw new DashSpecParseException(
                $"Include kind must be diagram, presentation, chrome, transform, or tooltip, got '{includeKind}'."),
        };
    }

    private static SpecIncludeFragment LoadTooltipFragment(string text)
    {
        var (id, definition) = TooltipModuleParser.ParseTooltipFileWithId(text);
        return new SpecIncludeFragment(
            null,
            null,
            null,
            new Dictionary<string, TooltipDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [id] = definition,
            });
    }

    public static SpecIncludeFragment Merge(SpecIncludeFragment current, SpecIncludeFragment incoming)
    {
        return new SpecIncludeFragment(
            MergeDiagram(current.Diagram, incoming.Diagram),
            MergePresentation(current.Presentation, incoming.Presentation),
            MergeSeriesTransform(current.SeriesTransform, incoming.SeriesTransform),
            MergeTooltips(current.Tooltips, incoming.Tooltips),
            InspectPresentationParser.Merge(current.Inspect, incoming.Inspect));
    }

    private static bool IsStdlibReference(string reference) =>
        reference.Length >= 2 && reference[0] is '<' && reference[^1] is '>';

    private static string GetStdlibRoot()
    {
        if (!string.IsNullOrWhiteSpace(_stdlibRootOverride))
        {
            return _stdlibRootOverride;
        }

        var assemblyDir = Path.GetDirectoryName(typeof(SpecIncludeResolver).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(assemblyDir))
        {
            var nextToAssembly = Path.Combine(assemblyDir, "stdlib");
            if (Directory.Exists(nextToAssembly))
            {
                return nextToAssembly;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "stdlib");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var coreCandidate = Path.Combine(dir.FullName, "src", "DashSpec.Core", "stdlib");
            if (Directory.Exists(coreCandidate))
            {
                return coreCandidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("DashSpec stdlib directory was not found.");
    }

    private static string ResolveExistingFile(string path, string includeKind)
    {
        if (File.Exists(path))
        {
            return path;
        }

        var extensions = includeKind.ToLowerInvariant() switch
        {
            "diagram" => new[] { ".dashdiagram" },
            "presentation" or "chrome" => new[] { ".dashpresentation" },
            "transform" => new[] { ".dashtransform" },
            "tooltip" => new[] { ".dashtooltip" },
            _ => Array.Empty<string>(),
        };

        foreach (var ext in extensions)
        {
            var withExt = path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? path : path + ext;
            if (File.Exists(withExt))
            {
                return withExt;
            }
        }

        return path;
    }

    private static DiagramDefinition? MergeDiagram(DiagramDefinition? left, DiagramDefinition? right)
    {
        if (right is null)
        {
            return left;
        }

        if (left is null || (string.IsNullOrWhiteSpace(left.Kind) && string.IsNullOrWhiteSpace(left.UsePreset)))
        {
            return right;
        }

        var props = new Dictionary<string, string>(left.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in right.Properties)
        {
            props[key] = value;
        }

        var kind = string.IsNullOrWhiteSpace(right.Kind) ? left.Kind : right.Kind;
        var preset = right.UsePreset ?? left.UsePreset;
        return new DiagramDefinition(kind, props, preset);
    }

    private static PresentationBlock? MergePresentation(PresentationBlock? left, PresentationBlock? right)
    {
        if (right is null)
        {
            return left;
        }

        if (left is null)
        {
            return right;
        }

        var use = right.UsePreset ?? left.UsePreset;
        var inline = new Dictionary<string, string>(left.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in right.Properties)
        {
            inline[key] = value;
        }

        return new PresentationBlock(use, inline);
    }

    private static SeriesTransformBlock? MergeSeriesTransform(SeriesTransformBlock? left, SeriesTransformBlock? right)
    {
        if (right is null)
        {
            return left;
        }

        if (left is null)
        {
            return right;
        }

        return new SeriesTransformBlock(
            right.UsePreset ?? left.UsePreset,
            right.Max ?? left.Max,
            right.OtherLabel ?? left.OtherLabel);
    }

    private static IReadOnlyDictionary<string, TooltipDefinition>? MergeTooltips(
        IReadOnlyDictionary<string, TooltipDefinition>? left,
        IReadOnlyDictionary<string, TooltipDefinition>? right)
    {
        if (right is null || right.Count == 0)
        {
            return left;
        }

        if (left is null || left.Count == 0)
        {
            return right;
        }

        var merged = new Dictionary<string, TooltipDefinition>(left, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in right)
        {
            merged[key] = value;
        }

        return merged;
    }
}
