# DashSpec (DSPEC)

Декларативные operational dashboards: **текстовый `.dashspec` в git** → **интерактивный Blazor Server host**.

Проект AI Guiders — product-neutral DSL и Blazor host; **без привязки к конкретной БД** в core.

> **Стабильность:** версия **0.x**, синтаксис `.dashspec` и API Core могут меняться.
> Ломающие правки — через ADR в `design/`; после стабилизации DSL → public **1.0**.

## Быстрый старт

```powershell
git clone https://github.com/AI-Guiders/dash-spec.git
cd dash-spec
dotnet run --project src/DashSpec.Host
```

→ **http://localhost:5295** (по умолчанию грузит `samples/demo/demo-soak.dashspec`)

### Bootstrap

1. Host `dash-spec.toml` — только путь к `.dashspec`
2. `.dashspec` — `@config` (обязательно), опционально `@sqldialect`, `@diagramlibrary`
3. TOML из `@config` — connectors + plugins (самодостаточный)

`samples/demo/demo.toml` рядом с demo spec:

```toml
[connectors.sqlserver]
connection_string = "Server=...;Database=DashSpecDemo;Trusted_Connection=True;TrustServerCertificate=True"

[plugins]
default_connector_id = "sqlserver"

[[plugins.load]]
id = "sqlserver"
assembly = "DashSpec.Connector.SqlServer.dll"
```

Без `@config` в spec host выдаст понятную ошибку.

Reference sample: [`samples/demo/`](samples/demo/) — вымышленная схема `demo.v_*`.

## Структура (v0.2)

| Проект | Назначение |
|--------|------------|
| `DashSpec.Abstractions` | `IConnectorPlugin`, `IDataSourceConnector`, `CompiledQuery` |
| `DashSpec.Core` | parser, **фильтры**, `QueryCompiler`, chart payloads |
| `DashSpec.Connector.SqlServer` | plugin dll |
| `DashSpec.Host` | loader + Blazor UI |
| `samples/demo/` | reference `.dashspec` + diagram library |

## Где живут фильтры

**Не в коннекторе.** См. **[docs/FILTERS_RU.md](docs/FILTERS_RU.md)**:

- объявление → `filter …` в `.dashspec`
- привязка к карточке → `bind usage_date, app_name` (SQL компилируется из bind)
- значения на экране → `FilterState` в host
- SQL → `QueryCompiler` в Core

## DSL

```text
dashboard "Title" {
  connector sqlserver
  filter date usage_date on usage_date as "Report date" default -7d..today
  filter field app_name on demo.v_daily_active_users.app_name as "Products"

  card peak as "Peak" {
    bind usage_date, app_name
    use demo_peak_concurrent
  }
}
```

- `default -7d..today` — диапазон **в spec**; см. [FILTERS_RU.md](docs/FILTERS_RU.md)
- `bind` на card — фильтры карточки; Core строит `WHERE` / `TOP` ([ADR-0009](design/DASHSPEC-ADR-0009-bind-only-filters.md))
- `datasource view` — default; `datasource sql "…"` — escape hatch ([ADR-0006](design/DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md))
- `presentation { }` / `transform series { }` + `@diagramlibrary` — chrome и top-N ([ADR-0007](design/DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md))

## Design

- [design/DASHSPEC-ADR-0001-connectors-as-plugins.md](design/DASHSPEC-ADR-0001-connectors-as-plugins.md)

## Тесты

```powershell
dotnet test DashSpec.slnx
```
