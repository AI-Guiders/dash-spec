using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class IncludeExpander
{
    public static void Expand(
        string reference,
        string specDirectory,
        DocumentModuleKind moduleKind,
        ModuleIncludeState state,
        bool tolerateIncompleteIncludes = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(specDirectory);

        if (tolerateIncompleteIncludes && IncludeReferenceHeuristics.IsIncomplete(reference))
        {
            return;
        }

        foreach (var path in ResolvePaths(reference, specDirectory))
        {
            ExpandFile(path, specDirectory, moduleKind, state, tolerateIncompleteIncludes);
        }
    }

    private static void ExpandFile(
        string path,
        string specDirectory,
        DocumentModuleKind moduleKind,
        ModuleIncludeState state,
        bool tolerateIncompleteIncludes)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"!include not found: '{path}'.", path);
        }

        var extension = Path.GetExtension(path);
        switch (extension.ToLowerInvariant())
        {
            case ".dashlayout":
                AssignLayoutBoard(LayoutModuleParser.ParseLayoutFile(File.ReadAllText(path)), moduleKind, state);
                return;

            case ".dashdiagram":
                RegisterDiagramFile(path, specDirectory, state);
                return;

            case ".dashinclude":
                ExpandDashInclude(path, specDirectory, moduleKind, state, tolerateIncompleteIncludes);
                return;

            case ".dashpresentation":
                RegisterPresentationFile(path, specDirectory, state);
                return;

            case ".dashtransform":
                throw new DashSpecParseException(
                    $"!include '{path}': register transform via .dashdiagram or card block, not module include.");

            default:
                throw new DashSpecParseException(
                    $"!include '{path}': unsupported extension '{extension}'.");
        }
    }

    private static void ExpandDashInclude(
        string path,
        string specDirectory,
        DocumentModuleKind moduleKind,
        ModuleIncludeState state,
        bool tolerateIncompleteIncludes)
    {
        var reader = ParserUtilities.CreateReader(File.ReadAllText(path));
        reader.SkipNewlines();
        if (reader.IsAt(TokenKind.At))
        {
            reader.Advance();
            if (reader.TryKeyword("include"))
            {
                _ = reader.ReadIdent();
                reader.SkipNewlines();
            }
        }

        while (!reader.IsEof)
        {
            if (reader.TryModuleInclude(out var nested))
            {
                Expand(nested, Path.GetDirectoryName(path) ?? specDirectory, moduleKind, state, tolerateIncompleteIncludes);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("layout"))
            {
                var layoutReference = reader.ReadString();
                var layoutPath = SpecIncludeResolver.ResolvePath(layoutReference, specDirectory);
                layoutPath = ResolveLayoutPath(layoutPath);
                AssignLayoutBoard(
                    LayoutModuleParser.ParseLayoutFile(File.ReadAllText(layoutPath)),
                    moduleKind,
                    state);
                reader.SkipNewlines();
                continue;
            }

            if (reader.TryKeyword("diagram"))
            {
                var diagramReference = reader.ReadString();
                var diagramPath = SpecIncludeResolver.ResolvePath(diagramReference, specDirectory);
                diagramPath = ResolveDiagramPath(diagramPath);
                RegisterDiagramFile(diagramPath, specDirectory, state);
                reader.SkipNewlines();
                continue;
            }

            throw reader.Unexpected();
        }
    }

    private static void RegisterDiagramFile(string path, string specDirectory, ModuleIncludeState state)
    {
        var baseDirectory = Path.GetDirectoryName(path) ?? specDirectory;
        var (id, fragment) = DiagramModuleParser.ParseDiagramFileWithId(File.ReadAllText(path), baseDirectory);
        state.RegisterDiagram(id, fragment);
    }

    private static void RegisterPresentationFile(string path, string specDirectory, ModuleIncludeState state)
    {
        var baseDirectory = Path.GetDirectoryName(path) ?? specDirectory;
        var (id, block) = PresentationModuleParser.ParsePresentationFileWithId(
            File.ReadAllText(path),
            baseDirectory);
        state.RegisterChartChromePreset(id, block);
    }

    private static void AssignLayoutBoard(
        LayoutBoardDefinition board,
        DocumentModuleKind moduleKind,
        ModuleIncludeState state)
    {
        if (moduleKind is DocumentModuleKind.Dashboard)
        {
            state.AssignToolbarBoard(board, "!include layout (dashboard toolbar)");
            return;
        }

        state.AssignLayoutBoard(board, "!include layout (tab board)");
    }

    private static IEnumerable<string> ResolvePaths(string reference, string specDirectory)
    {
        if (!reference.Contains('*', StringComparison.Ordinal))
        {
            return [ResolveExistingIncludePath(reference, specDirectory)];
        }

        var combined = Path.GetFullPath(Path.Combine(specDirectory, reference));
        var directory = Path.GetDirectoryName(combined)
            ?? throw new DashSpecParseException($"!include glob has no directory: '{reference}'.");
        var pattern = Path.GetFileName(combined);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new DashSpecParseException($"!include glob requires a file pattern: '{reference}'.");
        }

        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .GetFiles(directory, pattern)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveExistingIncludePath(string reference, string specDirectory)
    {
        var path = SpecIncludeResolver.ResolvePath(reference, specDirectory);
        if (File.Exists(path))
        {
            return path;
        }

        foreach (var ext in new[] { ".dashlayout", ".dashdiagram", ".dashinclude", ".dashpresentation" })
        {
            var withExt = path.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? path : path + ext;
            if (File.Exists(withExt))
            {
                return withExt;
            }
        }

        return path;
    }

    private static string ResolveLayoutPath(string path) =>
        File.Exists(path) ? path :
        File.Exists(path + ".dashlayout") ? path + ".dashlayout" : path;

    private static string ResolveDiagramPath(string path) =>
        File.Exists(path) ? path :
        File.Exists(path + ".dashdiagram") ? path + ".dashdiagram" : path;
}
