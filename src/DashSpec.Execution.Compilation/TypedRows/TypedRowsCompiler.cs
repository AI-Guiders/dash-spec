using System.Reflection;
using System.Text;
using DashSpec.Abstractions.Data;
using DashSpec.Modeling.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DashSpec.Execution.Compilation;

/// <summary>
/// ADR-0124: compiles spec row types into a real CLR assembly — <c>dsc build types</c>.
/// Types-units are parsed by the F# SSOT model; this emits one C# POCO per row type
/// (field kinds → CLR types, a constructor from the DashValue wire) plus a
/// <c>RowTypeRegistry</c> (name → factory), and compiles with Roslyn
/// (<c>CSharpCompilation</c>) into <c>TypedRows.dll</c> next to the corpus. No runtime
/// type generation: the hot path is one registry lookup.
/// </summary>
public static class TypedRowsCompiler
{
    public record BuildResult(string AssemblyPath, IReadOnlyList<string> TypeNames);

    public static BuildResult Compile(
        IReadOnlyList<RowTypeDef> definitions,
        string outputDirectory,
        string assemblyName = "DashSpec.TypedRows")
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(outputDirectory);

        // Composite join types carry side-qualified fields (`r.col`, ADR-0126 §2) —
        // a SQL-facing surface, not CLR row shape (no runtime code constructs them:
        // managed views read the database). They ship in the .ds catalog; the CLR
        // assembly carries the plain-identifier types only.
        var clrTypes = definitions
            .Where(d => d.Fields.All(f => !f.Name.Contains('.')))
            .ToList();

        var source = EmitSource(clrTypes);
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, assemblyName + ".dll");

        // The wire contract assembly + the SDK ref pack (no implementation assemblies:
        // System.Private.CoreLib next to ref assemblies breaks predefined type resolution).
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(DashValue).Assembly.Location),
        };
        references.AddRange(FrameworkReferences());

        var syntax = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntax],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage()));
            throw new InvalidOperationException($"TypedRows compilation failed:\n{errors}");
        }

        return new BuildResult(path, clrTypes.Select(d => d.Name).ToList());
    }

    /// <summary>Framework reference assemblies for compilation: the SDK's
    /// Microsoft.NETCore.App.Ref pack when present, else the app directory's facades.</summary>
    private static IEnumerable<MetadataReference> FrameworkReferences()
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        if (!Directory.Exists(dotnetRoot))
        {
            dotnetRoot = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        }

        var refPack = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
        if (Directory.Exists(refPack))
        {
            // the emitted assembly runs on THIS process's runtime — a ref pack of a
            // newer major (e.g. 11 packs after an SDK 11 install) makes the host
            // unable to load it (System.Runtime 11 vs net10 runtime)
            var runtimeMajor = Environment.Version.Major;
            var version = Directory.EnumerateDirectories(refPack)
                .Where(d => Version.TryParse(Path.GetFileName(d), out _))
                .Where(d => Version.Parse(Path.GetFileName(d)).Major == runtimeMajor)
                .OrderByDescending(d => Version.Parse(Path.GetFileName(d)))
                .Select(d => Path.Combine(d, "ref"))
                .SelectMany(refRoot => Directory.EnumerateDirectories(refRoot))
                .FirstOrDefault();
            if (version is not null)
            {
                foreach (var file in Directory.EnumerateFiles(version, "*.dll"))
                {
                    yield return MetadataReference.CreateFromFile(file);
                }

                yield break;
            }
        }

        foreach (var facade in new[] { "System.Runtime.dll", "System.Linq.dll", "System.Collections.dll", "netstandard.dll" })
        {
            var file = Path.Combine(AppContext.BaseDirectory, facade);
            if (File.Exists(file))
            {
                yield return MetadataReference.CreateFromFile(file);
            }
        }
    }

    /// <summary>Scans a corpus directory for types-units (<c>module types &lt;id&gt;</c>) and
    /// parses them with the F# SSOT parser. Flow units (<c>module flow &lt;id&gt;</c>) ride
    /// along (ADR-0127): their port type inference may synthesize row types, and those
    /// compile into the same TypedRows.dll — hand-written and inferred types are
    /// indistinguishable for the runtime.</summary>
    public static IReadOnlyList<RowTypeDef> LoadCorpusTypes(string directory)
    {
        var definitions = new Dictionary<string, RowTypeDef>(StringComparer.OrdinalIgnoreCase);
        var flowFiles = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.ds", SearchOption.AllDirectories))
        {
            var header = FirstLines(file, 2);
            if (header.Contains("module types", StringComparison.Ordinal))
            {
                foreach (var definition in ParseTypes(DashSpec.Modeling.Parse.Document.UnitSource.readUnit(file)))
                {
                    definitions[definition.Name] = definition;
                }
            }
            else if (header.Contains("module flow", StringComparison.Ordinal))
            {
                flowFiles.Add(file);
            }
        }

        if (flowFiles.Count > 0)
        {
            var catalog = DashSpec.Modeling.Core.TypeCatalogModule.ofDefinitions(definitions.Values);
            foreach (var file in flowFiles)
            {
                var module = DashSpec.Modeling.Parse.DataFlow.DashflowModuleParser.parseModule(
                    DashSpec.Modeling.Parse.Document.UnitSource.readUnit(file), catalog);
                foreach (var inferred in module.InferredRowTypes)
                {
                    definitions[inferred.Name] = inferred;
                }
            }
        }

        return definitions.Values.ToList();
    }

    /// <summary>The project marker (*.dsproj, ADR-0101) is the one layout authority.
    /// Missing? Create it — root_namespace derived from the first flow unit's own
    /// namespace (Ai.Landscape.Flows → Ai.Landscape) — and reload. One path, no
    /// fallback layout.</summary>
    public static DashSpec.Modeling.Parse.Project.DashSpecProjectConfig EnsureProjectConfig(string directory, IReadOnlyList<string> flowFiles)
    {
        var opt = DashSpec.Modeling.Parse.Project.DashSpecProjectLocator.tryFindConfig(
            Microsoft.FSharp.Core.FSharpOption<string>.Some(directory));

        if (Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Project.DashSpecProjectConfig>.get_IsSome(opt))
        {
            return opt.Value;
        }

        string? derived = null;
        foreach (var file in flowFiles)
        {
            var nsOpt = DashSpec.Modeling.Parse.Document.UnitSource.namespaceOf(File.ReadAllText(file));
            if (Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(nsOpt))
            {
                var ns = nsOpt.Value;
                derived = ns.EndsWith(".Flows", StringComparison.Ordinal) ? ns[..^".Flows".Length] : ns;
                break;
            }
        }

        var marker = Path.Combine(directory, Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar)) + ".dsproj");
        var markerText = derived is null
            ? "root_namespace = \"Generated\"\n"
            : $"root_namespace = \"{derived}\"\n";
        File.WriteAllText(marker, markerText);
        Console.WriteLine($"project: created {marker}" + (derived is null ? "" : $" (root_namespace = \"{derived}\")"));

        var created = DashSpec.Modeling.Parse.Project.DashSpecProjectLocator.tryFindConfig(
            Microsoft.FSharp.Core.FSharpOption<string>.Some(directory));
        return Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Project.DashSpecProjectConfig>.get_IsSome(created)
            ? created.Value
            : throw new InvalidOperationException($"project marker created but not discovered: {marker}");
    }

    /// <summary>dsc generate types (ADR-0128): emit inferred row types as ordinary
    /// .ds types-units. Existing types are updated in-place in whichever types-unit
    /// AST currently declares them; brand-new types default to <c>types/&lt;name&gt;.ds</c>.
    /// Regeneration merges by name (ADR-0128 §3 step 3); generated units are ordinary
    /// seed-catalog members, so the twin preference carries the author's renames.
    /// The round-trip law guards the printer.</summary>
    public static int GenerateTypesUnit(string directory, string moduleId, string? namespaceName)
    {
        // the seed catalog is the AUTHOR's declared types only — including previously
        // generated units (they are ordinary types units). We retain the parsed AST
        // of every loaded types-unit in memory and track which file declares each type
        // so member updates mutate the AST where the type actually lives.
        var declared = new Dictionary<string, RowTypeDef>(StringComparer.OrdinalIgnoreCase);
        var typeLocations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var loadedUnitAsts = new Dictionary<string, DashSpec.Modeling.Parse.Syntax.DashSpecAstNode>(StringComparer.OrdinalIgnoreCase);
        var flowFiles = new List<string>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.ds", SearchOption.AllDirectories))
        {
            var header = FirstLines(file, 2);

            if (header.Contains("module types", StringComparison.Ordinal))
            {
                var fullPath = Path.GetFullPath(file);
                var rawText = DashSpec.Modeling.Parse.Document.UnitSource.tryReadRaw(file).Value;
                var unitRoot = DashSpec.Modeling.Parse.Syntax.DashSpecSurfaceSyntax.parse(rawText).Root;
                loadedUnitAsts[fullPath] = unitRoot;
                var projectedOpt = DashSpec.Modeling.Parse.Types.TypeModuleParser.tryProjectTypesFromAst(unitRoot);
                var definitions = Microsoft.FSharp.Core.FSharpOption<RowTypeDef[]>.get_IsSome(projectedOpt)
                    ? projectedOpt.Value
                    : ParseTypes(rawText);
                foreach (var definition in definitions)
                {
                    declared[definition.Name] = definition;
                    typeLocations[definition.Name] = fullPath;
                }
            }
            else if (header.Contains("module flow", StringComparison.Ordinal))
            {
                flowFiles.Add(file);
            }
        }

        var catalog = DashSpec.Modeling.Core.TypeCatalogModule.ofDefinitions(declared.Values);

        // the project marker (*.dsproj, ADR-0101) owns the layout — created when
        // missing; new generated units default to <project>/types/<name>.ds, namespace = root_namespace
        var projectConfig = EnsureProjectConfig(directory, flowFiles);
        var typesDir = Path.Combine(projectConfig.ProjectRoot, "types");
        Directory.CreateDirectory(typesDir);

        var ns = namespaceName;
        if (string.IsNullOrWhiteSpace(ns)
            && projectConfig.RootNamespace is { } root
            && Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(root)
            && !string.IsNullOrWhiteSpace(root.Value))
        {
            ns = root.Value + ".Types";
        }

        var nsOption = string.IsNullOrWhiteSpace(ns)
            ? Microsoft.FSharp.Core.FSharpOption<string>.None
            : Microsoft.FSharp.Core.FSharpOption<string>.Some(ns);

        var candidates = new List<RowTypeDef>();

        foreach (var file in flowFiles)
        {
            var text = DashSpec.Modeling.Parse.Document.UnitSource.readUnit(file);
            var module = DashSpec.Modeling.Parse.DataFlow.DashflowModuleParser.parseModule(text, catalog);
            candidates.AddRange(module.InferredRowTypes);
        }

        if (candidates.Count == 0)
        {
            Console.Error.WriteLine("no inferred row types found under " + directory);
            return 1;
        }

        var dirtyUnits = new Dictionary<string, (string UnitId, List<RowTypeDef> ExpectedTypes)>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            // If the type already lives in any loaded types-unit (regardless of file name or layout),
            // update it in-place in that exact unit; otherwise place new types into types/<name>.ds.
            var targetPath = typeLocations.TryGetValue(candidate.Name, out var existingPath)
                ? existingPath
                : Path.GetFullPath(Path.Combine(typesDir, $"{candidate.Name}.ds"));

            if (loadedUnitAsts.TryGetValue(targetPath, out var existingAst))
            {
                loadedUnitAsts[targetPath] = DashSpec.Modeling.Parse.Syntax.DashSpecAstPrinter.mergeTypeIntoUnit(existingAst, candidate);
            }
            else
            {
                loadedUnitAsts[targetPath] = DashSpec.Modeling.Parse.Syntax.DashSpecAstPrinter.synthesizeTypesUnit(candidate.Name, nsOption, [candidate]);
            }

            typeLocations[candidate.Name] = targetPath;

            if (!dirtyUnits.TryGetValue(targetPath, out var entry))
            {
                var unitId = Path.GetFileNameWithoutExtension(targetPath).Replace('.', '_');
                entry = (unitId, new List<RowTypeDef>());
                dirtyUnits[targetPath] = entry;
            }

            entry.ExpectedTypes.RemoveAll(t => string.Equals(t.Name, candidate.Name, StringComparison.OrdinalIgnoreCase));
            entry.ExpectedTypes.Add(candidate);
        }

        foreach (var (targetPath, (unitId, expectedTypes)) in dirtyUnits)
        {
            FlushTypesUnitAst(unitId, loadedUnitAsts[targetPath], expectedTypes, targetPath);
        }

        return dirtyUnits.Count == 0 ? 1 : 0;
    }

    /// <summary>Project an in-memory types-unit AST to text, verify the round-trip law, and write to disk.</summary>
    private static void FlushTypesUnitAst(
        string unitId,
        DashSpec.Modeling.Parse.Syntax.DashSpecAstNode unitAst,
        IReadOnlyList<RowTypeDef> expectedTypes,
        string targetPath)
    {
        var unitText = DashSpec.Modeling.Parse.Syntax.DashSpecAstPrinter.print(unitAst);

        try
        {
            var reparsed = ParseTypes(unitText).ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var expected in expectedTypes)
            {
                if (!reparsed.TryGetValue(expected.Name, out var actual)
                    || !DashSpec.Modeling.Core.TypeCatalogModule.rowTypesStructurallyEqual(expected, actual))
                {
                    Console.Error.WriteLine($"round-trip law broken for '{unitId}' — the printer produced text that does not reparse to the same definitions");
                    Console.Error.WriteLine(unitText);
                    throw new InvalidOperationException("round-trip law broken (see the dumped text above).");
                }
            }
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            Console.Error.WriteLine($"round-trip law broken for '{unitId}': {ex.Message}");
            Console.Error.WriteLine(unitText);
            throw;
        }

        File.WriteAllText(targetPath, unitText);
        Console.WriteLine($"OK: {targetPath} ({expectedTypes.Count} row types: {string.Join(", ", expectedTypes.Select(d => d.Name))})");
    }

    /// <summary>Scans a corpus for view-marked flow units (<c>module flow &lt;id&gt; view</c>,
    /// ADR-0125) and emits their chains as managed views. Types are loaded first so the
    /// emitter can render explicit column lists and inference (ADR-0127) can contribute
    /// synthesized row types.</summary>
    public static IReadOnlyList<Views.ManagedView> LoadCorpusViews(string directory, string dialect = "tsql")
    {
        var types = LoadCorpusTypes(directory);
        var catalog = DashSpec.Modeling.Core.TypeCatalogModule.ofDefinitions(types);
        var views = new List<Views.ManagedView>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.ds", SearchOption.AllDirectories))
        {
            var header = FirstLines(file, 2);
            if (!header.Contains("module flow", StringComparison.Ordinal))
            {
                continue;
            }

            var module = DashSpec.Modeling.Parse.DataFlow.DashflowModuleParser.parseModule(
                DashSpec.Modeling.Parse.Document.UnitSource.readUnit(file), catalog);

            // Composite row types synthesized during this parse ride the module; the
            // emitter merges them into the working catalog itself (ManagedViews SSOT).
            views.AddRange(Views.ManagedViewsEmitter.Emit(module, catalog, dialect));
        }

        return views;
    }

    private static IEnumerable<RowTypeDef> ParseTypes(string text) =>
        DashSpec.Modeling.Parse.Types.TypeModuleParser.parseTypesModule(text);

    private static string FirstLines(string path, int count)
    {
        using var reader = new StreamReader(path);
        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (reader.ReadLine() is { } line)
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    internal static string EmitSource(IReadOnlyList<RowTypeDef> definitions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated> dsc build types — row types compiled from the spec. </auto-generated>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace DashSpec.TypedRows;");
        builder.AppendLine();
        builder.AppendLine("""
            public static class RowCells
            {
                public static string? ToString(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsString();
                public static int? ToInt(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsInt32();
                public static decimal? ToDecimal(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsDecimal();
                public static bool? ToBool(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsBool();
                public static System.DateOnly? ToDate(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsDate();
                public static System.TimeOnly? ToTime(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsTime();
                public static System.DateTime? ToDateTime(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsDateTimeUtc();
                public static System.TimeSpan? ToDuration(DashSpec.Abstractions.Data.DashValue v) => v.IsNull ? null : v.AsDuration();
            }

            public static class RowTypeRegistry
            {
                private static readonly System.Collections.Generic.Dictionary<string, System.Func<DashSpec.Abstractions.Data.DashValue[], string[], object>> Factories = new(System.StringComparer.OrdinalIgnoreCase);

                public static bool Contains(string name) => Factories.ContainsKey(name);

                public static object Create(string name, DashSpec.Abstractions.Data.DashValue[] cells, string[] fieldNames) => Factories[name](cells, fieldNames);

                static RowTypeRegistry()
                {
            """);

        foreach (var definition in definitions)
        {
            builder.AppendLine($"        Factories[\"{definition.Name}\"] = (cells, names) => new {Sanitize(definition.Name)}(cells, names);");
        }

        builder.AppendLine("""
                }
            }
            """);

        foreach (var definition in definitions)
        {
            builder.AppendLine();
            builder.AppendLine($"public sealed class {Sanitize(definition.Name)}");
            builder.AppendLine("{");
            foreach (var field in definition.Fields)
            {
                builder.AppendLine($"    public {ClrType(field)} {Sanitize(field.Name)} {{ get; set; }}");
            }

            builder.AppendLine();
            builder.AppendLine($"    public {Sanitize(definition.Name)}(DashSpec.Abstractions.Data.DashValue[] cells, string[] fieldNames)");
            builder.AppendLine("    {");
            builder.AppendLine("        for (var i = 0; i < fieldNames.Length && i < cells.Length; i++)");
            builder.AppendLine("        {");
            builder.AppendLine("            switch (fieldNames[i])");
            builder.AppendLine("            {");
            foreach (var field in definition.Fields)
            {
                builder.AppendLine($"                case \"{field.Name}\":");
                builder.AppendLine($"                    {Sanitize(field.Name)} = {WireRead(field)}(cells[i]);");
                builder.AppendLine("                    break;");
            }

            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    private static string WireRead(RowFieldDef field) => PrimitiveName(field) switch
    {
        "bool" => "RowCells.ToBool",
        "int" => "RowCells.ToInt",
        "decimal" => "RowCells.ToDecimal",
        "string" => "RowCells.ToString",
        "duration" => "RowCells.ToDuration",
        "date" => "RowCells.ToDate",
        "time" => "RowCells.ToTime",
        "datetime" => "RowCells.ToDateTime",
        var unknown => throw new InvalidOperationException($"no wire mapping for primitive '{unknown}'"),
    };

    private static string ClrType(RowFieldDef field) => PrimitiveName(field) switch
    {
        "bool" => "bool?",
        "int" => "int?",
        "decimal" => "decimal?",
        "string" => "string?",
        "duration" => "System.TimeSpan?",
        "date" => "System.DateOnly?",
        "time" => "System.TimeOnly?",
        "datetime" => "System.DateTime?",
        var unknown => throw new InvalidOperationException($"no CLR mapping for primitive '{unknown}'"),
    };

    /// <summary>The primitive keyword through the F# SSOT (<c>DashPrimitive.toString</c>);
    /// non-primitive field types are loud — only primitives compile (ADR-0079).</summary>
    private static string PrimitiveName(RowFieldDef field)
    {
        if (field.Type is not DashType.Primitive primitive)
        {
            throw new InvalidOperationException(
                $"row type '{field.Name}': only primitive fields compile to CLR.");
        }

        return DashPrimitiveModule.toString(primitive.Item);
    }

    private static string Sanitize(string identifier)
    {
        var valid = identifier.Length > 0
            && (char.IsLetter(identifier[0]) || identifier[0] == '_')
            && identifier.All(c => char.IsLetterOrDigit(c) || c == '_');
        if (!valid)
        {
            throw new InvalidOperationException(
                $"'{identifier}' is not a plain identifier — cannot compile to a CLR member name.");
        }

        return identifier;
    }
}
