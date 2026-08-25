# DashSpec (DSPEC)

Декларативные operational dashboards: **текстовый `.dashspec` в git** → **интерактивный Blazor Server host**.

Проект AI Guiders — product-neutral DSL и Blazor host; **без привязки к конкретной БД** в Core.

> **Early preview (0.x):** DSL и API Core могут меняться между минорными версиями.
> Ломающие правки — через ADR в `design/`; стабильный контракт — с public **1.0**.
> См. [CHANGELOG.md](CHANGELOG.md).

## Документация для людей

| Документ | Для кого |
|----------|----------|
| **[docs/AUTHORING_GUIDE_RU.md](docs/AUTHORING_GUIDE_RU.md)** | Authoring Guide: модель, файлы, bind, клики, dogfood |
| **[docs/HOWTO_RU.md](docs/HOWTO_RU.md)** | How-to: локальный Host, первый card, кросс-фильтры, catalog, runtime, служба |
| [docs/README.md](docs/README.md) | Оглавление всей папки `docs/` |

## Быстрый старт

```powershell
git clone https://github.com/AI-Guiders/dash-spec.git
cd dash-spec
dotnet run --project src/DashSpec.Host
```

→ **http://localhost:5295** (по умолчанию `samples/demo/demo-catalog.dashcatalog`, entry `demo_soak`)

Нужна SQL Server с demo-схемой — см. [`samples/demo/demo.toml`](samples/demo/demo.toml) и `demo.local.toml.example`.

### Bootstrap

1. Host `dash-spec.toml` — `[dashboard] catalog_path` → `.dashcatalog`
2. `.dashcatalog` — whitelist отчётов; `default` entry → первый экран
3. `.dashspec` — `@module` + `runtime { manifest = "…" }`, `configuration { }`, `body` / `dashboard { }` ([ADR-0024](design/DASHSPEC-ADR-0024-document-authoring-layers.md)); legacy: flat `@runtime`, `@sqldialect`
4. TOML из `runtime.manifest` — connectors + plugins (deployment manifest, не DSL)

`samples/demo/demo.toml`:

```toml
[connectors.sqlserver]
connection_string = "Server=...;Database=DashSpecDemo;Trusted_Connection=True;TrustServerCertificate=True"
command_timeout_seconds = 120   # SqlCommand timeout; 0 = connector default (120)
max_rows = 250000               # abort if result exceeds row count; 0 = default (250000)

[plugins]
default_connector_id = "sqlserver"

[[plugins.load]]
id = "sqlserver"
assembly = "DashSpec.Connector.SqlServer.dll"
```

Без `@runtime` host выдаст понятную ошибку. `@config` — deprecated alias.

### Доступ (prod)

В host TOML (`dash-spec.local.toml`, не в `@runtime` продукта):

```toml
[access]
api_key = "CHANGE_ME"
```

Или env `DASHSPEC_API_KEY`. Пустой ключ — host открыт (dev по умолчанию).

- Браузер: `/access` → ключ → HttpOnly cookie на 30 дней
- API/скрипты: заголовок `X-Api-Key`
- Закладка: `/?api_key=…` (один раз, cookie без query)
- `GET /health` — без ключа (мониторинг службы)

Reference sample: [`samples/demo/`](samples/demo/) — вымышленная схема `demo.v_*`, файловые `diagrams/` / `palettes/`.

## Scope (0.x)

| Есть сейчас | Пока нет |
|-------------|----------|
| Blazor Server host, hot reload spec-файлов | PostgreSQL / другие коннекторы (только plugin model) |
| SqlServer connector plugin | `place` на фильтрах (toolbar — только board/ref) |
| Line, bar, table, heatmap, pie/donut charts | Полный language reference на EN (RU: docs/AUTHORING_GUIDE + HOWTO + DIAGRAM_KINDS_ROADMAP) |
| Модульные `@tab`, file includes, layout boards | CI badge, packaged NuGet |

## Структура

| Проект | Назначение |
|--------|------------|
| `DashSpec.Abstractions` | `IConnectorPlugin`, `IDataSourceConnector`, `CompiledQuery` |
| `DashSpec.Core` | parser, фильтры, layout/toolbar boards, `QueryCompiler`, chart payloads |
| `DashSpec.Connector.SqlServer` | единственный bundled connector (plugin dll) |
| `DashSpec.Host` | loader + Blazor UI (CSS grid для cards и toolbar) |
| `samples/demo/` | reference `.dashspec` + `diagrams/` / `palettes/` |
| `design/` | ADR (архитектурные решения DSL и host) |
| `docs/` | Authoring Guide, How-to, FILTERS, authoring |
| `editor/` | VS Code extension ([`editor/vscode-dashspec`](editor/vscode-dashspec/)); authoring: [`docs/authoring`](docs/authoring/README.md) |

## Authoring (editor)

Подсветка, **LSP** (diagnostics, completion, go-to-definition) и snippets для `.dashspec` и родственных файлов — [`editor/vscode-dashspec`](editor/vscode-dashspec/). Перед F5: `scripts/publish-language-server.ps1` и `npm install` в каталоге extension.  
Grammar в редакторе **может отставать** от парсера; для проверки — `DashSpec.Host validate` или validate on save в extension.

## Где живут фильтры

**Не в коннекторе.** См. **[docs/FILTERS_RU.md](docs/FILTERS_RU.md)**:

- объявление → `filter …` в `.dashspec`
- привязка к карточке → `bind usage_date, app_name` (SQL компилируется из bind)
- значения на экране → `FilterState` в host
- SQL → `QueryCompiler` в Core

## DSL (кратко)

### Dashboard shell

```text
@runtime "demo.toml"
@sqldialect tsql
@palette "palettes/demo-apps.dashpalette"

@dashboard demo_soak

dashboard "Title" {
  connector sqlserver
  layout grid { columns = 12; gap = 16 }

  filter date usage_date on usage_date as "Report date" default -7d..today
  filter field app_name on demo.v_daily_active_users.app_name as "Products"

  toolbar chrome { layout = bar; sticky = line; apply = auto }

  tab overview as "Overview" { cards { peak, dau } }
  tab analytics dashspec "demo-analytics.dashspec"

  card peak as "Peak" {
    bind usage_date, app_name
    include diagram "diagrams/peak-concurrent-line.dashdiagram"
    datasource view demo.v_daily_peak_concurrent_proxy
  }
}
```

- `default -7d..today` — диапазон **в spec**; см. [FILTERS_RU.md](docs/FILTERS_RU.md)
- `bind` на card — фильтры карточки; Core строит `WHERE` / `TOP` ([ADR-0009](design/DASHSPEC-ADR-0009-bind-only-filters.md))
- `datasource view` — default; `datasource sql query` / `datasource sql file` ([ADR-0018](design/DASHSPEC-ADR-0018-sql-datasource-carriers.md))

### Модульность и file includes

Один язык — несколько корней файлов ([ADR-0017](design/DASHSPEC-ADR-0017-file-includes-and-stdlib.md)). **Грамматика документа** (`runtime { }`, `configuration { }`, `imports { }`, `wiring { }`, `body { }`) — [ADR-0024](design/DASHSPEC-ADR-0024-document-authoring-layers.md).

| Расширение | Корень | Содержимое |
|------------|--------|------------|
| `.dashspec` | `@dashboard` / `@tab` | dashboard, filters, cards, tabs |
| `.dashcatalog` | `@catalog` | whitelist отчётов для Host ([ADR-0023](design/DASHSPEC-ADR-0023-dashcatalog.md)) |
| `.dashdiagram` | `@diagram` | `diagram { }`, опционально presentation/transform |
| `.dashpresentation` | `@presentation` | layout chart area |
| `.dashpalette` | `@palette` | цвета серий |
| `.dashlayout` | `@layout` | bracket board `[ Q W ]` |

**Вкладка в отдельном файле** — `@tab` module ([ADR-0011](design/DASHSPEC-ADR-0011-tab-modules.md)):

```text
@tab analytics
@runtime "demo.toml"

card period_peak as "Peak by period" {
  include diagram "diagrams/period-peak-by-app-bar.dashdiagram"
  datasource view demo.v_peak_concurrent_by_period
  bind period_grain, period_start, app_name
}
```

В parent: `tab analytics dashspec "demo-analytics.dashspec"`.

### Catalog отчётов (`.dashcatalog`)

Whitelist верхнего уровня для Host ([ADR-0023](design/DASHSPEC-ADR-0023-dashcatalog.md)):

```text
@catalog lus_dev
default soak

entry soak as "License Usage — Soak"
  dashspec "lus-dev-soak.dashspec"

entry stakeholder as "Отчёты заказчика"
  dashspec "lus-dev-stakeholder.dashspec"
```

Host bootstrap:

```toml
[dashboard]
catalog_path = "path/to/catalogs/lus-dev.dashcatalog"
```

Зритель переключает отчёт в dropdown; автор добавляет `entry` в git.

### Layout карточек (tab board)

Короткий `ref` на card + ASCII-сетка на вкладке ([ADR-0020](design/DASHSPEC-ADR-0020-card-ref-and-layout-board.md)):

```text
card stakeholder_peak_by_app as "Peak" ref Q { ... }

tab stakeholder as "Reports" {
  layout {
    [ Q W ]
    [ E ]
  }
}
```

Вынести board в файл ([ADR-0021](design/DASHSPEC-ADR-0021-dashlayout-include.md)):

```text
include layout "layouts/stakeholder-grid.dashlayout"
```

```text
@layout stakeholder_grid
scope tab

[ Q W ]
[ E ]
```

### Toolbar (filter ref + board)

Та же bracket-модель для фильтров ([ADR-0022](design/DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md)):

```text
filter date usage_date on usage_date as "Date" ref D default -7d..today
filter field app_name on demo.v_daily_active_users.app_name as "Products" ref A

toolbar {
  [ D A ]
  [ U   ]
}

include toolbar "layouts/soak-toolbar.dashlayout"
```

Legacy `toolbar { usage_date, app_name }` — одна неявная строка (совместимость).

## Design

Ключевые ADR:

- [ADR-0001](design/DASHSPEC-ADR-0001-connectors-as-plugins.md) — connectors as plugins
- [ADR-0011](design/DASHSPEC-ADR-0011-tab-modules.md) — `@tab` modules
- [ADR-0017](design/DASHSPEC-ADR-0017-file-includes-and-stdlib.md) — file includes
- [ADR-0020](design/DASHSPEC-ADR-0020-card-ref-and-layout-board.md) — card `ref`, tab layout board
- [ADR-0021](design/DASHSPEC-ADR-0021-dashlayout-include.md) — `.dashlayout`
- [ADR-0022](design/DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) — toolbar board
- [ADR-0026](design/DASHSPEC-ADR-0026-layout-module-scope.md) — mandatory `scope` in `.dashlayout`
- [ADR-0027](design/DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md) — single declaration; layout tokens = filter/card id (proposed)
- [ADR-0029](design/DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md) — tooltip/inspect in presentation, not diagram (proposed)
- [ADR-0023](design/DASHSPEC-ADR-0023-dashcatalog.md) — `.dashcatalog`
- [ADR-0024](design/DASHSPEC-ADR-0024-document-authoring-layers.md) — document blocks (`runtime`, `configuration`, `wiring`, `body`)

Полный список — каталог [`design/`](design/).

## Тесты

```powershell
dotnet test DashSpec.slnx
```

## Лицензия

Software: [MIT](LICENSE) ([канонический текст](https://github.com/AI-Guiders/licensing/blob/main/software/MIT.txt)) · Ethical use: [policy](https://github.com/AI-Guiders/licensing/blob/main/docs/ethical-use.md)
