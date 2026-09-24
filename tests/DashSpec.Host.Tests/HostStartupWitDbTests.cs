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
        var previousDb = Environment.GetEnvironmentVariable("DASHSPEC_HOST_DB");
        try
        {
            SeedWitDb(witdb, scenario);
            Environment.SetEnvironmentVariable("DASHSPEC_HOST_DB", witdb);

            var environment = CreateProductionEnvironment(HandoffRoot);
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
            Environment.SetEnvironmentVariable("DASHSPEC_HOST_DB", previousDb);
            TryDeleteWitDb(witdb);
        }
    }

    [Fact]
    public void Production_startup_allows_catalog_usage_write_after_sequence()
    {
        var witdb = Path.Combine(Path.GetTempPath(), $"dashspec-startup-{Guid.NewGuid():N}", "host-settings.witdb");
        var previousDb = Environment.GetEnvironmentVariable("DASHSPEC_HOST_DB");
        try
        {
            SeedWitDb(witdb, WitDbScenario.LegacyCatalogUsage);
            Environment.SetEnvironmentVariable("DASHSPEC_HOST_DB", witdb);

            var environment = CreateProductionEnvironment(HandoffRoot);
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
            Environment.SetEnvironmentVariable("DASHSPEC_HOST_DB", previousDb);
            TryDeleteWitDb(witdb);
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
        try
        {
            var dir = Path.GetDirectoryName(witdbPath);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
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
