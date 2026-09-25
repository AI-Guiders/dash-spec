using DashSpec.Host.Data;
using DashSpec.Host.Services.Settings;
using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;
using Xunit;

namespace DashSpec.Host.Tests;

public class CatalogUsageServiceTests
{
    [Fact]
    public void RecordSelection_fails_when_catalog_usage_has_legacy_snake_case_columns()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dashspec-catalog-{Guid.NewGuid():N}.witdb");
        try
        {
            var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
                .UseWitDb($"Data Source={dbPath}")
                .Options;
            using (var db = new DashSpecHostDbContext(options))
            {
                db.Database.EnsureCreated();
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
            }

            using var db2 = new DashSpecHostDbContext(options);
            var service = new CatalogUsageService(db2);

            Assert.Throws<DbUpdateException>(() => service.RecordSelection("client-a", "stakeholder"));
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void RecordSelection_works_after_EnsureCreated_only()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dashspec-catalog-{Guid.NewGuid():N}.witdb");
        try
        {
            var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
                .UseWitDb($"Data Source={dbPath}")
                .Options;
            using var db = new DashSpecHostDbContext(options);
            db.Database.EnsureCreated();
            new CatalogUsageService(db).RecordSelection("client-a", "stakeholder");
            Assert.Equal(1, db.CatalogUsage.Count());
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void EnsureDatabase_repairs_legacy_snake_case_catalog_usage_table()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dashspec-catalog-{Guid.NewGuid():N}.witdb");
        try
        {
            var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
                .UseWitDb($"Data Source={dbPath}")
                .Options;
            using (var db = new DashSpecHostDbContext(options))
            {
                db.Database.EnsureCreated();
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
            }

            HostTestServices.CreateHostDatabase().EnsureDatabase(dbPath);

            using var db2 = new DashSpecHostDbContext(options);
            new CatalogUsageService(db2).RecordSelection("client-a", "stakeholder");
            Assert.Equal(1, db2.CatalogUsage.Count());
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void EnsureDatabase_can_be_called_twice_without_opening_witdb_twice()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dashspec-catalog-{Guid.NewGuid():N}.witdb");
        try
        {
            var hostDatabase = HostTestServices.CreateHostDatabase();
            hostDatabase.EnsureDatabase(dbPath);
            var exception = Record.Exception(() => hostDatabase.EnsureDatabase(dbPath));
            Assert.Null(exception);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void RecordSelection_after_EnsureDatabase_persists_row()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dashspec-catalog-{Guid.NewGuid():N}.witdb");
        try
        {
            HostTestServices.CreateHostDatabase().EnsureDatabase(dbPath);

            var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
                .UseWitDb($"Data Source={dbPath}")
                .Options;
            using var db = new DashSpecHostDbContext(options);
            var service = new CatalogUsageService(db);

            service.RecordSelection("client-a", "stakeholder");

            Assert.Equal(1, db.CatalogUsage.Count());
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
