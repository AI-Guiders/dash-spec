namespace DashSpec.Host.Data;

public sealed class CatalogUsageEntity
{
    public string ClientId { get; set; } = string.Empty;

    public string EntryId { get; set; } = string.Empty;

    public int HitCount { get; set; }

    public DateTimeOffset LastUsedAt { get; set; }
}
