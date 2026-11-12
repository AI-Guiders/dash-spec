using System.Text.RegularExpressions;
using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Plugins;
using DashSpec.Publishing.SqlServer;
using DashSpec.Composition.Plugins;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HostModuleParser = DashSpec.Execution.Parsing.HostModuleParser;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Cli;

/// <summary>
/// dsc — CLI composition root (DASHSPEC-ADR-0117 steps 3–4). The web Host kept `validate`
/// as a redirect; dsc owns spec validation from here. Vocabulary parity with the Host is
/// mandatory: the same plugin kernel (<see cref="DashSpecPluginComposition"/>) fed from the
/// same <c>dash-spec.toml</c> policy — a spec with extension blocks from external plugins
/// is checked by the same language the runtime speaks.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length is 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "validate" => Validate(args.Skip(1).ToArray()),
            "build" => Build(args.Skip(1).ToArray()),
            "generate" => Generate(args.Skip(1).ToArray()),
            var unknown => Unknown(unknown),
        };
    }

    /// <summary>dsc generate types &lt;dir&gt; — emit the flow-inferred row
    /// types as ordinary types-unit .ds files (the authored surface). Regeneration is
    /// a structural member-level merge in whichever types-unit currently declares the
    /// type; after the first emission the compiler prefers the authored twin and
    /// synthesizes nothing.</summary>
    private static int Generate(string[] rest)
    {
        if (rest.Length < 2 || rest[0] != "types" || rest[1] is "--help" or "-h")
        {
            Console.Error.WriteLine("usage: dsc generate types <corpus-dir> [--namespace <ns>]");
            return 2;
        }

        var fullPath = Path.GetFullPath(rest[1]);
        var moduleId = "inferred_types";
        string? namespaceName = null;

        for (var i = 2; i < rest.Length - 1; i++)
        {
            if (rest[i] == "--module")
            {
                moduleId = rest[i + 1];
            }
            else if (rest[i] == "--namespace")
            {
                namespaceName = rest[i + 1];
            }
        }

        if (!Directory.Exists(fullPath))
        {
            Console.Error.WriteLine($"Directory not found: {fullPath}");
            return 2;
        }

        return TypedRowsCompiler.GenerateTypesUnit(fullPath, moduleId, namespaceName);
    }

    /// <summary>dsc build types &lt;dir&gt; — ADR-0124: compile the corpus's row types into
    /// TypedRows.dll next to the catalog (Roslyn, no runtime generation).
    /// dsc build views &lt;dir&gt; — ADR-0125: emit managed views from `module flow … view`
    /// chains; --print (default) reviews the DDL, --connection applies it (the publishing
    /// account owns the DDL rights, the host stays a read-only client).</summary>
    private static int Build(string[] rest)
    {
        if (rest.Length is 0 || rest[0] is "--help" or "-h")
        {
            Console.Error.WriteLine("usage: dsc build types <corpus-dir> [--out <dir>]");
            Console.Error.WriteLine("       dsc build views <corpus-dir> [--dialect tsql|postgres] [--project-uid <id>] [--connection <conn-str>]");
            return 2;
        }

        return rest[0].ToLowerInvariant() switch
        {
            "types" => BuildTypes(rest.Skip(1).ToArray()),
            "views" => BuildViews(rest.Skip(1).ToArray()),
            var unknown => UnknownBuildTarget(unknown),
        };
    }

    private static int UnknownBuildTarget(string target)
    {
        Console.Error.WriteLine($"unknown build target '{target}' — expected: types | views");
        return 2;
    }

    private static int BuildTypes(string[] rest)
    {
        var dir = rest.Length > 0 ? rest[0] : ".";
        var outDir = rest.Length > 2 && rest[1] is "--out" ? rest[2] : dir;
        var fullPath = Path.GetFullPath(dir);
        if (!Directory.Exists(fullPath))
        {
            Console.Error.WriteLine($"Directory not found: {fullPath}");
            return 2;
        }

        var types = TypedRowsCompiler.LoadCorpusTypes(fullPath);
        if (types.Count == 0)
        {
            Console.Error.WriteLine($"No types-units (module types …) found under {fullPath}");
            return 1;
        }

        var result = TypedRowsCompiler.Compile(types, Path.GetFullPath(outDir));
        Console.WriteLine($"OK: {result.AssemblyPath} ({result.TypeNames.Count} row types: {string.Join(", ", result.TypeNames)})");
        return 0;
    }

    private static int BuildViews(string[] rest)
    {
        string? dir = null;
        string? connection = null;
        string dialect = "tsql";
        string projectUid = "dsc-cli";
        string? sqlLayout = null;
        var prune = false;
        for (var i = 0; i < rest.Length; i++)
        {
            if (rest[i] is "--prune")
            {
                prune = true;
            }
            else if (rest[i] is "--connection" && i + 1 < rest.Length)
            {
                connection = rest[++i];
            }
            else if (rest[i] is "--dialect" && i + 1 < rest.Length)
            {
                dialect = rest[++i];
            }
            else if (rest[i] is "--layout" && i + 1 < rest.Length)
            {
                sqlLayout = rest[++i];
            }
            else if (rest[i] is "--project-uid" && i + 1 < rest.Length)
            {
                projectUid = rest[++i];
            }
            else
            {
                dir ??= rest[i];
            }
        }

        var fullPath = Path.GetFullPath(dir ?? ".");
        if (!Directory.Exists(fullPath))
        {
            Console.Error.WriteLine($"Directory not found: {fullPath}");
            return 2;
        }

        var views = TypedRowsCompiler.LoadCorpusViews(fullPath, dialect);
        if (views.Count == 0)
        {
            Console.Error.WriteLine($"No view-marked flow units (module flow … view) found under {fullPath}");
            return 1;
        }

        var sqlDir = WriteSqlFiles(views, fullPath, dialect, sqlLayout);
        foreach (var view in views)
        {
            Console.WriteLine($"-- {view.ViewName}");
            Console.WriteLine(view.Ddl);
            Console.WriteLine();
        }

        if (connection is null)
        {
            Console.WriteLine($"OK: {views.Count} managed view(s) emitted to {sqlDir} (print mode — pass --connection to apply)");
            return 0;
        }

        if (!dialect.Equals("tsql", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"apply mode supports tsql so far (dialect '{dialect}' prints only)");
            return 1;
        }

        // publish = the connector's write face (ADR-0085: SQL lives in acquisition);
        // the CLI resolves the publisher by dialect until ConnectorRegistry grows a
        // capability lookup — same shape Studio will ride
        IManagedViewPublisher publisher = dialect.Equals("tsql", StringComparison.OrdinalIgnoreCase)
            ? new SqlServerViewPublisher()
            : throw new InvalidOperationException($"dialect '{dialect}' has no managed-view publisher.");

        publisher.Apply(
            views.Select(v => new ManagedViewDdl(v.ViewName, v.Ddl)).ToList(),
            connection,
            projectUid);
        if (prune)
        {
            var dropped = publisher.PruneOrphans(connection, projectUid, views.Select(v => v.ViewName).ToHashSet(StringComparer.OrdinalIgnoreCase));
            Console.WriteLine($"prune: dropped {dropped} orphaned view(s)");
        }

        Console.WriteLine($"OK: {views.Count} managed view(s) applied + leased to '{projectUid}', DDL in {sqlDir}");
        return 0;
    }

    /// <summary>The emitted artifact is an ordinary project file: readable DDL under
    /// <c>sql/views/</c> — git diffs show real SQL, SSMS/DataGrip can open it, a DBA
    /// pipeline can run it. Layout is an option: file-per-view (default — one .sql
    /// per managed view, <c>sql/views/&lt;name&gt;.sql</c>) or single-file (one .sql
    /// per flow). No manifest: the compile-time reader computes view names from the
    /// parsed .ds itself (ManagedViews SSOT).</summary>
    private static string WriteSqlFiles(IReadOnlyList<DashSpec.Execution.Compilation.Views.ManagedView> views, string corpusDir, string dialect, string? layout)
    {
        // the project marker (*.dsproj, ADR-0101) owns the layout: DDL lives under
        // <project>/sql/views — created when missing, same one-path rule as types
        var config = TypedRowsCompiler.EnsureProjectConfig(corpusDir, new List<string>());
        var sqlDir = Path.Combine(config.ProjectRoot, "sql", "views");
        Directory.CreateDirectory(sqlDir);

        var perView = !string.Equals(layout, "single-file", StringComparison.OrdinalIgnoreCase);

        if (perView)
        {
            foreach (var view in views)
            {
                var path = Path.Combine(sqlDir, $"{view.ViewName}.sql");
                var body = $"-- {view.FlowId}/{view.ChainId}{(view.IsFunction ? " (iTVF)" : "")}{Environment.NewLine}{view.Ddl}";
                File.WriteAllText(path, body + Environment.NewLine);
            }
        }
        else
        {
            foreach (var group in views.GroupBy(v => v.FlowId, StringComparer.OrdinalIgnoreCase))
            {
                var path = Path.Combine(sqlDir, $"{group.Key}.sql");
                var body = string.Join(
                    Environment.NewLine + Environment.NewLine,
                    group.Select(v => $"-- {v.ChainId}{(v.IsFunction ? " (iTVF)" : "")}{Environment.NewLine}{v.Ddl}"));
                File.WriteAllText(path, body + Environment.NewLine);
            }
        }

        return sqlDir;
    }

    private static int Validate(string[] rest)
    {
        string? path = null;
        string? tomlPath = null;
        for (var i = 0; i < rest.Length; i++)
        {
            if (rest[i] is "--toml" && i + 1 < rest.Length)
            {
                tomlPath = rest[++i];
            }
            else if (rest[i].StartsWith("--toml=", StringComparison.Ordinal))
            {
                tomlPath = rest[i]["--toml=".Length..];
            }
            else
            {
                path ??= rest[i];
            }
        }

        if (path is null)
        {
            Console.Error.WriteLine("usage: dsc validate <path> [--toml <dash-spec.toml>]");
            return 2;
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"File not found: {fullPath}");
            return 2;
        }

        if (Path.GetExtension(fullPath).Equals(".toml", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Runtime manifests are not specs; validate the .ds file that references them.");
            return 2;
        }

        try
        {
            // Same bootstrap defaults as the web root (ADR-0111 §2 / ADR-0117 decision 3):
            // the embedded host.ds grid serves documents that draw no grid of their own.
            DashSpecParser.EnsureModuleParsersRegistered();
            DashSpec.Composition.Shell.DashSpecDefaultShell.RegisterGridDefault();

            switch (Classify(fullPath))
            {
                case UnitKind.HostModule:
                    var hostDoc = HostModuleParser.ParseFile(fullPath);
                    var hostDir = Path.GetDirectoryName(fullPath)!;
                    var catalogPath = Path.GetFullPath(Path.Combine(hostDir, hostDoc.CatalogPath));
                    DashSpecValidator.ValidateCatalog(catalogPath, BuildParseOptions(tomlPath));
                    break;
                case UnitKind.Catalog:
                    DashSpecValidator.ValidateCatalog(fullPath, BuildParseOptions(tomlPath));
                    break;
                case UnitKind.Runtime:
                    _ = DashSpec.Modeling.Parse.Runtime.RuntimeModuleParser.parseFile(fullPath);
                    break;
                default:
                    ValidateSpec(fullPath, BuildParseOptions(tomlPath));
                    break;
            }

            Console.WriteLine($"OK: {fullPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Content-first classification (ADR-0117 step 3): the module root declares the unit —
    /// `module host`, `module catalog`, everything else validates as spec. Legacy extension
    /// dispatch is retired together with the legacy formats (ADR-0101).
    /// </summary>
    private static UnitKind Classify(string path)
    {
        using var reader = new StreamReader(path);
        for (var read = 0; read < 64; read++)
        {
            var line = reader.ReadLine();
            if (line is null)
            {
                break;
            }

            var match = Regex.Match(line, @"^\s*module\s+(?<kind>[A-Za-z]+)\b");
            if (match.Success)
            {
                return match.Groups["kind"].Value.ToLowerInvariant() switch
                {
                    "host" => UnitKind.HostModule,
                    "catalog" => UnitKind.Catalog,
                    "runtime" => UnitKind.Runtime,
                    _ => UnitKind.Spec,
                };
            }
        }

        // No module header: the spec branch reports the honest parse error.
        return UnitKind.Spec;
    }

    private static void ValidateSpec(string fullPath, DashSpec.Compiler.Contract.DashSpecParseOptions parseOptions)
    {
        var specDirectory = Path.GetDirectoryName(fullPath)!;
        DashSpecValidator.ValidateSpec(fullPath, specDirectory, parseOptions);
    }

    /// <summary>Vocabulary parity with the web root (ADR-0117 decision 3): the same plugin
    /// kernel, the same dash-spec.toml policy — spec and catalog entries alike.</summary>
    private static DashSpec.Compiler.Contract.DashSpecParseOptions BuildParseOptions(string? tomlPath)
    {
        var manifest = LoadManifest(tomlPath);
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var pluginsDir = ResolvePluginsDirectory();

        var kernel = DashSpecPluginComposition.Compose(services, configuration, manifest, pluginsDir);
        return new DashSpecParseOptionsProvider(kernel.Registry).CreateOptions();
    }

    private static DashSpecPluginManifest LoadManifest(string? tomlPath)
    {
        if (tomlPath is not null)
        {
            return DashSpecPluginsToml.Read(Path.GetFullPath(tomlPath));
        }

        var probed = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "dash-spec.toml"),
            Path.Combine(Directory.GetCurrentDirectory(), "dash-spec.local.toml"),
            Path.Combine(AppContext.BaseDirectory, "dash-spec.toml"),
        }.FirstOrDefault(File.Exists);

        if (probed is null)
        {
            return new DashSpecPluginManifest
            {
                ActiveBundle = "standard",
                DefaultProviderId = "sqlserver",
                Plugins =
                [
                    new DashSpecPluginLoadEntry
                    {
                        Id = "card_export",
                        Assembly = "DashSpec.Plugin.Export.dll",
                        Tier = DashSpec.Abstractions.Plugins.PluginTier.Extended,
                    },
                ],
            };
        }

        return DashSpecPluginsToml.Read(probed);
    }

    private static string? ResolvePluginsDirectory()
    {
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        return Directory.Exists(pluginsDir) ? pluginsDir : null;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            dsc — DashSpec CLI

            Usage:
              dsc validate <path> [--toml <dash-spec.toml>]
                  Validate a .ds spec, host module or catalog. Content-first: the module
                  root classifies the unit. Plugin vocabulary mirrors the web Host.
              dsc generate types <dir> [--namespace <ns>]
                  Emit flow-inferred row types as ordinary .ds types-units.
              dsc build types <dir> [--out <dir>]
                  Compile row types from .ds units into TypedRows.dll.
              dsc build views <dir> [--dialect tsql|postgres] [--layout file-per-view|single-file] [--project-uid <id>] [--connection <connStr>] [--prune]
                  Emit or apply managed views from 'module flow <id> view' units.
            """);
    }

    private enum UnitKind
    {
        Spec,
        HostModule,
        Catalog,
        Runtime,
    }
}
