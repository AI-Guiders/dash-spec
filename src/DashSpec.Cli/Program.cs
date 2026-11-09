using System.Text.RegularExpressions;
using DashSpec.Abstractions.Plugins;
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
            var unknown => Unknown(unknown),
        };
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

        foreach (var view in views)
        {
            Console.WriteLine($"-- {view.ViewName}");
            Console.WriteLine(view.Ddl);
            Console.WriteLine();
        }

        if (connection is null)
        {
            Console.WriteLine($"OK: {views.Count} managed view(s) (print mode — pass --connection to apply)");
            return 0;
        }

        if (!dialect.Equals("tsql", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"apply mode supports tsql so far (dialect '{dialect}' prints only)");
            return 1;
        }

        ApplyViews(views, connection, projectUid);
        WriteManifest(views, fullPath);
        if (prune)
        {
            var dropped = PruneOrphans(connection, projectUid, views.Select(v => v.ViewName).ToHashSet(StringComparer.OrdinalIgnoreCase));
            Console.WriteLine($"prune: dropped {dropped} orphaned view(s)");
        }

        Console.WriteLine($"OK: {views.Count} managed view(s) applied + leased to '{projectUid}', manifest written");
        return 0;
    }

    /// <summary>The compiled artifact ships with the catalog (ADR-0125, the ADR-0124
    /// symmetry): the manifest maps chain terminals to physical view names — the
    /// reader's lookup at compile time.</summary>
    private static void WriteManifest(IReadOnlyList<DashSpec.Execution.Compilation.Views.ManagedView> views, string corpusDir)
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var entries = views.Select(v => new
        {
            flow = v.FlowId,
            chain = v.ChainId,
            viewName = v.ViewName,
            rowType = v.RowType ?? "",
            isFunction = v.IsFunction,
        });
        var path = Path.Combine(corpusDir, "dashspec.views.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(entries, options));
    }

    /// <summary>Drop dashspec_ views with no leases from other projects and absent
    /// from the current revision (the current set was just re-leased). Views leased
    /// by other projects survive — the whole point of leases (ADR-0125 §2).</summary>
    private static int PruneOrphans(string connection, string projectUid, HashSet<string> current)
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connection);
        conn.Open();

        var orphans = new List<string>();
        using (var find = conn.CreateCommand())
        {
            find.CommandText = """
                SELECT v.name
                FROM sys.views v
                WHERE v.name LIKE 'dashspec[_]%'
                  AND NOT EXISTS (
                    SELECT 1 FROM dashspec_view_leases l
                    WHERE l.view_name = v.name AND l.project_uid <> @project
                  )
                """;
            find.Parameters.AddWithValue("@project", projectUid);
            using var reader = find.ExecuteReader();
            while (reader.Read())
            {
                if (!current.Contains(reader.GetString(0)))
                {
                    orphans.Add(reader.GetString(0));
                }
            }
        }

        foreach (var orphan in orphans)
        {
            var safeName = orphan.Replace("]", "]]");
            using var drop = conn.CreateCommand();
            drop.CommandText = $"DROP VIEW IF EXISTS [{safeName}]";
            drop.ExecuteNonQuery();

            using var clearLease = conn.CreateCommand();
            clearLease.CommandText = "DELETE FROM dashspec_view_leases WHERE view_name = @view";
            clearLease.Parameters.AddWithValue("@view", orphan);
            clearLease.ExecuteNonQuery();
        }

        // stale lease rows for views that no longer exist (e.g. an interrupted run)
        using (var sweep = conn.CreateCommand())
        {
            sweep.CommandText = """
                DELETE l FROM dashspec_view_leases l
                WHERE NOT EXISTS (SELECT 1 FROM sys.views v WHERE v.name = l.view_name)
                  AND NOT EXISTS (SELECT 1 FROM sys.objects o WHERE o.name = l.view_name AND o.type IN ('IF', 'TF', 'FN'))
                """;
            sweep.ExecuteNonQuery();
        }

        return orphans.Count;
    }

    /// <summary>Publish-time DDL (ADR-0125): CREATE OR ALTER each view and refresh this
    /// project's leases — the reference-counting that makes a future --prune safe.</summary>
    private static void ApplyViews(
        IReadOnlyList<DashSpec.Execution.Compilation.Views.ManagedView> views,
        string connection,
        string projectUid)
    {
        using var conn = new Microsoft.Data.SqlClient.SqlConnection(connection);
        conn.Open();

        using (var leaseTable = conn.CreateCommand())
        {
            leaseTable.CommandText = """
                IF OBJECT_ID('dashspec_view_leases') IS NULL
                CREATE TABLE dashspec_view_leases (
                    view_name NVARCHAR(256) NOT NULL,
                    project_uid NVARCHAR(128) NOT NULL,
                    updated_at_utc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_dashspec_view_leases PRIMARY KEY (view_name, project_uid)
                )
                """;
            leaseTable.ExecuteNonQuery();
        }

        foreach (var view in views)
        {
            using var ddl = conn.CreateCommand();
            ddl.CommandText = view.Ddl;
            ddl.ExecuteNonQuery();

            using var lease = conn.CreateCommand();
            lease.CommandText = """
                MERGE dashspec_view_leases AS target
                USING (SELECT @view AS view_name, @project AS project_uid) AS src
                ON target.view_name = src.view_name AND target.project_uid = src.project_uid
                WHEN MATCHED THEN UPDATE SET updated_at_utc = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT (view_name, project_uid) VALUES (src.view_name, src.project_uid);
                """;
            lease.Parameters.AddWithValue("@view", view.ViewName);
            lease.Parameters.AddWithValue("@project", projectUid);
            lease.ExecuteNonQuery();
        }
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
                    HostModuleParser.ParseFile(fullPath);
                    break;
                case UnitKind.Catalog:
                    DashSpecValidator.ValidateCatalog(fullPath, BuildParseOptions(tomlPath));
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
            Console.Error.WriteLine(
                "note: no dash-spec.toml found (cwd, exe dir) — builtin vocabulary only; pass --toml for full parity.");
            return new DashSpecPluginManifest();
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
            dsc — DashSpec CLI (DASHSPEC-ADR-0117)

            Usage:
              dsc validate <path> [--toml <dash-spec.toml>]
                  Validate a .ds spec, host module or catalog. Content-first: the module
                  root classifies the unit. Plugin vocabulary mirrors the web Host: the
                  same kernel, the same dash-spec.toml policy (ADR-0117 decision 3).
            """);
    }

    private enum UnitKind
    {
        Spec,
        HostModule,
        Catalog,
    }
}
