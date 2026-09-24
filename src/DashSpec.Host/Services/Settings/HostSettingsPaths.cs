using System.Data;
using DashSpec.Host.Configuration;
using DashSpec.Host.Data;
using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace DashSpec.Host.Services.Settings;

public static class HostSettingsPaths
{
    public static string ResolveDatabasePath(DashSpecTomlRoot bootstrap)
    {
        var env = Environment.GetEnvironmentVariable("DASHSPEC_HOST_DB");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return Path.GetFullPath(env);
        }

        if (!string.IsNullOrWhiteSpace(bootstrap.Host.DatabasePath))
        {
            return Path.GetFullPath(bootstrap.Host.DatabasePath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DashSpec",
            "host-settings.witdb");
    }

    public static void EnsureDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
            .UseWitDb($"Data Source={databasePath}")
            .Options;
        using var db = new DashSpecHostDbContext(options);
        db.Database.EnsureCreated();
        EnsureCatalogUsageTable(db);
    }

    /// <summary>
    /// <see cref="EnsureCreated"/> does not add new tables to an existing WitDB file.
    /// An earlier host build created <c>catalog_usage</c> with snake_case columns via raw SQL;
    /// EF expects PascalCase property columns — repair on startup.
    /// </summary>
    private static void EnsureCatalogUsageTable(DashSpecHostDbContext db)
    {
        if (CatalogUsageSchemaAcceptsEfWrites(db))
        {
            return;
        }

        db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS catalog_usage;");
        db.ChangeTracker.Clear();

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE catalog_usage (
                ClientId TEXT NOT NULL,
                EntryId TEXT NOT NULL,
                HitCount INTEGER NOT NULL,
                LastUsedAt TEXT NOT NULL,
                CONSTRAINT PK_catalog_usage PRIMARY KEY (ClientId, EntryId)
            );
            """);
        db.Database.ExecuteSqlRaw(
            "CREATE INDEX IF NOT EXISTS IX_catalog_usage_ClientId ON catalog_usage (ClientId);");
    }

    /// <summary>
    /// WitDB treats legacy snake_case and EF PascalCase as the same for SELECT,
    /// but INSERT via EF only works when physical columns match the model.
    /// </summary>
    private static bool CatalogUsageSchemaAcceptsEfWrites(DashSpecHostDbContext db)
    {
        try
        {
            using var tx = db.Database.BeginTransaction();
            db.CatalogUsage.Add(new CatalogUsageEntity
            {
                ClientId = "__schema_probe__",
                EntryId = "__schema_probe__",
                HitCount = 0,
                LastUsedAt = DateTimeOffset.UnixEpoch,
            });
            db.SaveChanges();
            tx.Rollback();
            db.ChangeTracker.Clear();
            return true;
        }
        catch
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
