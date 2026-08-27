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
    }
}
