namespace DashSpec.Core.Resolution;

/// <summary>Host planet ops merge chains (ADR-0057 §8, ADR-0042, ADR-0054).</summary>
public static class HostOpsResolution
{
    /// <summary>WitDB live value wins over dashhost / TOML bootstrap.</summary>
    public static string ResolvePresentationSetting(
        string fallback,
        string? dashhostValue,
        string? witdbValue) =>
        DisplayResolution.ResolveChain(fallback, dashhostValue, witdbValue);

    public static string ResolveCatalogPath(
        string fallback,
        string? dashhostCatalog,
        string? tomlCatalog) =>
        DisplayResolution.ResolveChain(fallback, tomlCatalog, dashhostCatalog);

    public static string ResolveDisplayTimeZone(
        string sessionDefault,
        string? dashhostValue,
        string? witdbValue) =>
        ResolvePresentationSetting(sessionDefault, dashhostValue, witdbValue);

    public static string ResolveColorScheme(
        string fallback,
        string? dashhostValue,
        string? witdbValue) =>
        ResolvePresentationSetting(fallback, dashhostValue, witdbValue);

    public static string ResolveLanguage(
        string fallback,
        string? dashhostValue,
        string? witdbValue) =>
        ResolvePresentationSetting(fallback, dashhostValue, witdbValue);
}
