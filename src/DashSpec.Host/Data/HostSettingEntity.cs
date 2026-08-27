namespace DashSpec.Host.Data;

public sealed class HostSettingEntity
{
    public string Section { get; set; } = "";

    public string Key { get; set; } = "";

    public string Value { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
