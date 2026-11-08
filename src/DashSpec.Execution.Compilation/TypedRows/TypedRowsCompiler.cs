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

        var source = EmitSource(definitions);
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

        return new BuildResult(path, definitions.Select(d => d.Name).ToList());
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
            var version = Directory.EnumerateDirectories(refPack)
                .Where(d => Version.TryParse(Path.GetFileName(d), out _))
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
        builder.AppendLine("// <auto-generated> ADR-0124: dsc build types — row types compiled from the spec. </auto-generated>");
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
                $"row type '{field.Name}': only primitive fields compile to CLR (ADR-0079).");
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
