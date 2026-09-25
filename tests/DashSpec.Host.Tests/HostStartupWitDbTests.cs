using DashSpec.Host.Data;
using DashSpec.Host.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using DashSpec.Host.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OutWit.Database.EntityFramework.Extensions;
using Xunit;

namespace DashSpec.Host.Tests;

/// <summary>
/// Mirrors production startup: overlay WitDB then Program EnsureDatabase on one witdb path.
/// </summary>
public sealed class HostStartupWitDbTests
{
    [Theory]
    [InlineData(WitDbScenario.Fresh)]
    [InlineData(WitDbScenario.LegacyCatalogUsage)]
    [InlineData(WitDbScenario.Repaired)]
    public void Production_startup_sequence_does_not_throw(WitDbScenario scenario)
    {
        using var provider = HostTestServices.CreateProvider();
        var hostBootstrap = provider.GetRequiredService<IHostBootstrap>();
        var hostDatabase = provider.GetRequiredService<IHostDatabaseInitializer>();

        var witdb = Path.Combine(Path.GetTempPath(), $"dashspec-startup-{Guid.NewGuid():N}", "host-settings.witdb");
        var contentRoot = PrepareMinimalContentRoot(witdb);
        try
        {
            SeedWitDb(hostDatabase, witdb, scenario);

            var environment = CreateProductionEnvironment(contentRoot);
            var exception = Record.Exception(() =>
            {
                var bootstrap = hostBootstrap.LoadBootstrap(environment);
                var hostDbPath = hostDatabase.ResolveDatabasePath(bootstrap);
                hostDatabase.EnsureDatabase(hostDbPath);
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
        using var provider = HostTestServices.CreateProvider();
        var hostBootstrap = provider.GetRequiredService<IHostBootstrap>();
        var hostDatabase = provider.GetRequiredService<IHostDatabaseInitializer>();

        var witdb = Path.Combine(Path.GetTempPath(), $"dashspec-startup-{Guid.NewGuid():N}", "host-settings.witdb");
        var contentRoot = PrepareMinimalContentRoot(witdb);
        try
        {
            SeedWitDb(hostDatabase, witdb, WitDbScenario.LegacyCatalogUsage);

            var environment = CreateProductionEnvironment(contentRoot);
            var bootstrap = hostBootstrap.LoadBootstrap(environment);
            hostDatabase.EnsureDatabase(hostDatabase.ResolveDatabasePath(bootstrap));

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

    private static string PrepareMinimalContentRoot(string witdbPath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"dashspec-startup-{Guid.NewGuid():N}");
        var dashspecDir = Path.Combine(root, "dashspec");
        var catalogsDir = Path.Combine(dashspecDir, "catalogs");
        Directory.CreateDirectory(catalogsDir);

        File.WriteAllText(
            Path.Combine(catalogsDir, "demo.dashcatalog"),
            """
            @catalog demo
            entry overview dashspec "overview.dashspec"
            """);

        File.WriteAllText(
            Path.Combine(dashspecDir, "demo.dashhost"),
            """
            @host demo
            catalog "catalogs/demo.dashcatalog"
            end host
            """);

        var witdb = witdbPath.Replace('\\', '/');
        File.WriteAllText(
            Path.Combine(root, "dash-spec.local.toml"),
            $"""
            [host]
            dashhost = "dashspec/demo.dashhost"
            database_path = "{witdb}"

            [access]
            api_key = "test"
            """);
        return root;
    }

    private static TestHostEnvironment CreateProductionEnvironment(string contentRoot) =>
        new()
        {
            ContentRootPath = contentRoot,
            EnvironmentName = Environments.Production,
        };

    private static void SeedWitDb(IHostDatabaseInitializer hostDatabase, string witdbPath, WitDbScenario scenario)
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

        hostDatabase.EnsureDatabase(witdbPath);
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
}
