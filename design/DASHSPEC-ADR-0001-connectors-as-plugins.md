# DASHSPEC-ADR-0001: Коннекторы как plugins

| | |
|---|---|
| **Status** | Accepted · v0.2 |
| **Date** | 2026-06-24 |
| **Relates to** | Forge [FORGE-ADR-0014](https://github.com/AI-Guiders/agent-forge) (microkernel + plugins) |

## Context

v0.1: SQL Server был зашит в `DashSpec.Data`. Нужны другие источники (Postgres, mock, Parquet) без раздувания host.

**Фильтры** остаются в **Core + `.dashspec`** — это не зона коннектора (см. [docs/FILTERS_RU.md](../docs/FILTERS_RU.md)).

## Decision

### Слои

| Слой | Ответственность |
|------|-----------------|
| **DashSpec.Abstractions** | `IConnectorPlugin`, `IDataSourceConnector`, `CompiledQuery`, `ConnectorRegistry` |
| **DashSpec.Core** | parser, `FilterDefinition`, `QueryCompiler`, chart/table payloads |
| **DashSpec.Connector.*** | dll в `connectors/` |
| **DashSpec.Host** | plugin loader, Blazor UI фильтров, orchestration |

### Контракт

```csharp
public interface IConnectorPlugin
{
    string Id { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}

public interface IDataSourceConnector
{
    string Id { get; }
    Task<…> QueryAsync(CompiledQuery query, …);
    Task<…> QueryDistinctStringsAsync(string sql, …);
}
```

Manifest: TOML из **`@runtime`** в `.dashspec` (alias `@config` deprecated) — секции `[connectors]`, `[plugins]`, `[[plugins.load]]`.

Host bootstrap: `src/DashSpec.Host/dash-spec.toml` — **только** `[dashboard] spec_path` (где лежит `.dashspec`).

### Dashboard → connector

```text
@runtime "demo.toml"

@dashboard demo_soak
dashboard "…" {
  connector sqlserver
  …
}
```

- **`@runtime`** — **обязателен**. Путь к TOML manifest **относительно `.dashspec`**. Без `@runtime` host не стартует. (`@config` — deprecated alias, см. [ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md).)
- **`connector sqlserver`** — id плагина из `[plugins]` в том же TOML.

Пример `samples/demo/demo.toml`: `connection_string`, `plugins`, `[[plugins.load]]`.

Секреты: правка TOML локально, `dash-spec.local.toml` в `.gitignore` как отдельный `@runtime`, или env `Connectors__SqlServer__ConnectionString`.

## Non-goals v0.2

- Hot reload plugins
- Connector-specific filter types
- Forge host plugin (отдельный ADR)

## Consequences

- Новый backend = новая dll в `connectors/`, строка в manifest
- Фильтры не дублируются в connector config
- Demo sample: `samples/demo/demo.toml` через `@runtime` в `demo-soak.dashspec`
