using Microsoft.EntityFrameworkCore;

namespace DashSpec.Host.Data;

public sealed class DashSpecHostDbContext(DbContextOptions<DashSpecHostDbContext> options) : DbContext(options)
{
    public DbSet<HostSettingEntity> HostSettings => Set<HostSettingEntity>();

    public DbSet<CatalogUsageEntity> CatalogUsage => Set<CatalogUsageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogUsageEntity>(e =>
        {
            e.ToTable("catalog_usage");
            e.HasKey(x => new { x.ClientId, x.EntryId });
            e.Property(x => x.ClientId).HasMaxLength(64);
            e.Property(x => x.EntryId).HasMaxLength(128);
            e.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<HostSettingEntity>(e =>
        {
            e.ToTable("host_settings");
            e.HasKey(x => new { x.Section, x.Key });
            e.Property(x => x.Section).HasMaxLength(64);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.UpdatedBy).HasMaxLength(256);
            e.HasIndex(x => x.Section);
        });
    }
}
