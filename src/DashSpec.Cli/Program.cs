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
            var unknown => Unknown(unknown),
        };
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
