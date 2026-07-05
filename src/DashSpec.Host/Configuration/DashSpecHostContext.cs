namespace DashSpec.Host.Configuration;

/// <summary>Runtime-контекст Host, зафиксированный при старте (connectors/plugins из @runtime).</summary>
public sealed class DashSpecHostContext
{
    public required string StartupRuntimeConfigPath { get; init; }

    /// <summary>Значение @runtime у дефолтного entry (все entry catalog должны совпадать).</summary>
    public required string StartupRuntimeReference { get; init; }

    public required string DefaultSpecRelativePath { get; init; }

    /// <summary>Каталог дефолтного spec — fallback для @runtime у загруженных файлов.</summary>
    public required string DefaultSpecDirectory { get; init; }

    public required CatalogBootstrap Catalog { get; init; }
}
