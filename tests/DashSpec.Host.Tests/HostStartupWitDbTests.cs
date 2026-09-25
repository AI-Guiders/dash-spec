using DashSpec.Host.Configuration;
using DashSpec.Host.Data;
using DashSpec.Host.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OutWit.Database.EntityFramework.Extensions;
using Xunit;

namespace DashSpec.Host.Tests;

/// <summary>
/// Mirrors production startup: HostSettingsOverlay.Apply then Program EnsureDatabase on one witdb path.
/// </summary>
public sealed class HostStartupWitDbTests
{
    private static readonly string HandoffRoot = ResolveHandoffRoot();

    [Theory]
    [InlineData(WitDbScenario.Fresh)]
    [InlineData(WitDbScenario.LegacyCatalogUsage)]
    [InlineData(WitDbScenario.Repaired)]
    public void Production_startup_sequence_does_not_throw(WitDbScenario scenario)
    {
        var witdb = Path.Combine(Path.GetTempPath(), $"dashspec-startup-{Guid.NewGuid():N}", "host-settings.witdb");
        var contentRoot = PrepareTestContentRoot(witdb);
        try
        {
            SeedWitDb(witdb, scenario);

            var environment = CreateProductionEnvironment(contentRoot);
            var exception = Record.Exception(() =>
            {
                var bootstrap = DashSpecBootstrap.LoadBootstrap(environment);
                var hostDbPath = HostSettingsPaths.ResolveDatabasePath(bootstrap);
                HostSettingsPaths.EnsureDatabase(hostDbPath);
            });

            Assert.Null(exception);
        }
        finally
        {
            TryDeleteDirectory(contentRoot);
            TryDeleteWitDb(witdb);
        }
    }

    [Fact]
    public void Production_startup_allows_catalog_usage_write_after_sequence()
    {
        var witdb = Path.Combine(Path.GetTempPath(), $"dashspec-startup-{Guid.NewGuid():N}", "host-settings.witdb");
        var contentRoot = PrepareTestContentRoot(witdb);
        try
        {
            SeedWitDb(witdb, WitDbScenario.LegacyCatalogUsage);

            var environment = CreateProductionEnvironment(contentRoot);
            var bootstrap = DashSpecBootstrap.LoadBootstrap(environment);
            HostSettingsPaths.EnsureDatabase(HostSettingsPaths.ResolveDatabasePath(bootstrap));

            var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
                .UseWitDb($"Data Source={witdb}")
                .Options;
            using var db = new DashSpecHostDbContext(options);
            new CatalogUsageService(db).RecordSelection("client-smoke", "stakeholder");
            Assert.Equal(1, db.CatalogUsage.Count());
        }
        finally
        {
            TryDeleteDirectory(contentRoot);
            TryDeleteWitDb(witdb);
        }
    }

    private static string PrepareTestContentRoot(string witdbPath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"dashspec-handoff-{Guid.NewGuid():N}");
        CopyDirectory(HandoffRoot, root);
        var witdb = witdbPath.Replace('\\', '/');
        File.WriteAllText(
            Path.Combine(root, "dash-spec.local.toml"),
            $"""
            [host]
            database_path = "{witdb}"

            [access]
            api_key = "test"
            """);
        return root;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(source, destination));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static IHostEnvironment CreateProductionEnvironment(string contentRoot)
    {
        return new TestHostEnvironment
        {
            ContentRootPath = contentRoot,
            EnvironmentName = Environments.Production,
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot),
        };
    }

    private static void SeedWitDb(string witdbPath, WitDbScenario scenario)
    {
        if (scenario == WitDbScenario.Fresh)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
            .UseWitDb($"Data Source={witdbPath}")
            .Options;
        using var db = new DashSpecHostDbContext(options);
        db.Database.EnsureCreated();

        if (scenario == WitDbScenario.LegacyCatalogUsage)
        {
            db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS catalog_usage");
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE catalog_usage (
                    client_id TEXT NOT NULL,
                    entry_id TEXT NOT NULL,
                    hit_count INTEGER NOT NULL DEFAULT 0,
                    last_used_at TEXT NOT NULL,
                    PRIMARY KEY (client_id, entry_id)
                );
                """);
            return;
        }

        HostSettingsPaths.EnsureDatabase(witdbPath);
    }

    private static string ResolveHandoffRoot()
    {
        var candidates = new[]
        {
            @"D:\SSCADRepo\LogUseFunc.DashSpec\publish\handoff",
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "LogUseFunc.DashSpec", "publish", "handoff")),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(candidate, "dash-spec.toml"))
                && File.Exists(Path.Combine(candidate, "DashSpec.Host.dll")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Handoff publish folder not found. Run URSA build-dashspec-setup.ps1 before HostStartupWitDbTests.");
    }

    private static void TryDeleteWitDb(string witdbPath)
    {
        TryDeleteDirectory(Path.GetDirectoryName(witdbPath));
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temp smoke dirs.
        }
    }

    public enum WitDbScenario
    {
        Fresh,
        LegacyCatalogUsage,
        Repaired,
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "DashSpec.Host.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
