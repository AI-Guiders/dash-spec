namespace DashSpec.Host.Services.Loading;

public sealed class SpecLoadOptions
{
    public bool LoadFieldOptions { get; init; } = true;

    public TimeSpan FieldOptionsTimeout { get; init; } = TimeSpan.FromSeconds(20);
}
