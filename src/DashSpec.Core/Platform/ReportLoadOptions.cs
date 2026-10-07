namespace DashSpec.Core.Platform;

/// <summary>Options for report bootstrap (field options load, timeouts).</summary>
public class ReportLoadOptions
{
    public bool LoadFieldOptions { get; init; } = true;

    public TimeSpan FieldOptionsTimeout { get; init; } = TimeSpan.FromSeconds(20);
}
