# DashSpec (DSPEC)

Декларативные operational dashboards: **текстовые спеки `.ds` в git** → **DashSpec Platform** (engine) → **surfaces** (Web Host, Studio, …).

Проект AI Guiders — product-neutral DSL и BI platform; **без привязки к конкретной БД** в Core. **DashSpec.Host** — web **viewer**, не весь продукт ([ADR-0047](design/DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md)).

> **Early preview (0.x):** DSL и API Core могут меняться между минорными версиями.
> Ломающие правки — через ADR в `design/`; стабильный контракт — с public **1.0**.
> См. [CHANGELOG.md](CHANGELOG.md).

## Документация для людей

| Документ | Для кого |
|----------|----------|
| **[docs/ARCHITECTURE_RU.md](docs/ARCHITECTURE_RU.md)** | Архитектура решения: C4-модель, слои, конвейеры, сильные и слабые стороны (есть [DOCX](docs/architecture/DashSpec-Architecture-RU.docx)) |
| **[docs/AUTHORING_GUIDE_RU.md](docs/AUTHORING_GUIDE_RU.md)** | Authoring Guide: модель, файлы, bind, клики, dogfood |
| **[docs/HOWTO_RU.md](docs/HOWTO_RU.md)** | How-to: локальный Host, первый card, кросс-фильтры, catalog, runtime, служба |
| [docs/README.md](docs/README.md) | Оглавление всей папки `docs/` |

## Быстрый старт

```powershell
git clone https://github.com/AI-Guiders/dash-spec.git
cd dash-spec
dotnet run --project src/DashSpec.Host
```

→ **http://localhost:5295** (bootstrap: `src/DashSpec.Host/dash-spec.toml` → host-модуль `samples/demo/demo.ds` → каталог `demo-catalog.ds`, entry `demo_soak`)

Нужна SQL Server с demo-схемой — см. [`samples/demo/demo.toml`](samples/demo/demo.toml) и `demo.local.toml.example`. Локальный seed:

```powershell
sqlcmd -S localhost -d DashSpecDemo -E -i samples/demo/sql/bootstrap-demo-soak.sql
```

### Bootstrap

1. Host `dash-spec.toml` — `[host] dashhost` → **host-модуль `.ds`** (`module host`: chrome, `catalog`, `surfaces` — [ADR-0054](design/DASHSPEC-ADR-0054-dashhost-module.md))
2. Catalog-модуль `.ds` — whitelist отчётов (`entry … dashspec "*.ds"`); `default` entry → первый экран ([ADR-0023](design/DASHSPEC-ADR-0023-dashcatalog.md))
3. Dashboard/section `.ds` — типизированные модули ([ADR-0101](design/DASHSPEC-ADR-0101-single-unit-file-type.md)) с блоками `runtime { manifest = "…" }`, `configuration { }`, `report { }`
4. TOML из `runtime.manifest` — providers (deployment manifest, не DSL); viz/on-click плагины — `[plugins]` в host TOML (bundles)

`samples/demo/demo.toml`:

```toml
[providers.sqlserver]
connection_string = "Server=...;Database=DashSpecDemo;Trusted_Connection=True;TrustServerCertificate=True"
command_timeout_seconds = 120   # SqlCommand timeout; 0 = provider plugin default (120)
max_rows = 250000               # abort if result exceeds row count; 0 = default (250000)

[plugins]
default_provider_id = "sqlserver"

[[plugins.load]]
id = "sqlserver"
assembly = "DashSpec.Connector.SqlServer.dll"
is_provider = true
```

Без блока `runtime { manifest = … }` host выдаст понятную ошибку.

### Доступ (prod)

В host TOML (`dash-spec.local.toml`, не в спеке продукта):

```toml
[access]
api_key = "CHANGE_ME"
```

Пустой ключ — host открыт (dev по умолчанию).

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
| Line, area, bar, stacked bar, donut, table, heatmap, box, histogram, scatter, treemap, windrose, gauge, KPI, sparkline + gantt (viz-плагины) | Полный language reference на EN (RU: docs/AUTHORING_GUIDE + HOWTO + DIAGRAM_KINDS_ROADMAP) |
| Типизированные модули `.ds` ([ADR-0101](design/DASHSPEC-ADR-0101-single-unit-file-type.md)), namespaces/imports, flows, layout boards | CI badge, packaged NuGet |

## Структура

| Проект | Назначение |
|--------|------------|
| `DashSpec.Abstractions` | `IConnectorPlugin`, `IDataSourceConnector`, `CompiledQuery` |
| `DashSpec.Core` | parser, фильтры, layout/toolbar boards, `QueryCompiler`, chart payloads |
| `DashSpec.Connector.SqlServer` | единственный bundled connector (plugin dll) |
| `DashSpec.Host` | loader + Blazor UI (CSS grid для cards и toolbar) |
| `samples/demo/` | reference `.ds`-модули: host, catalog, dashboard, sections, flows, diagrams, palettes, types, tooltips |
| `design/` | ADR (архитектурные решения DSL и host) |
| `docs/` | Authoring Guide, How-to, FILTERS, authoring |
| `editor/` | VS Code extension ([`editor/vscode-dashspec`](editor/vscode-dashspec/)); authoring: [`docs/authoring`](docs/authoring/README.md) |

## Authoring (editor)

Подсветка, **LSP** (diagnostics, completion, go-to-definition) и snippets для `.dashspec` и родственных файлов — [`editor/vscode-dashspec`](editor/vscode-dashspec/). Перед F5: `scripts/publish-language-server.ps1` и `npm install` в каталоге extension.  
Grammar в редакторе **может отставать** от парсера; для проверки — `DashSpec.Host validate` или validate on save в extension.

## Где живут фильтры

**Не в коннекторе.** См. **[docs/FILTERS_RU.md](docs/FILTERS_RU.md)**:

- объявление → `filter …` в `.ds`
- привязка к карточке → `bind usage_date, app_name` (SQL компилируется из bind)
- значения на экране → `FilterState` в host
- SQL → `QueryCompiler` в Core

## DSL (кратко)

> Синтаксис сверен с живыми спеками `samples/demo/` (2026-11). Источник правды — парсер (`DashSpec.Host validate` / validate on save в editor-плагине); этот раздел и `docs/` могут отставать.

### Модульная модель (ADR-0101)

Один тип файла — **`.ds`**; вид модуля задаёт корень `module <вид> <id>`. Блоки закрываются `end`. Между модулями — `namespace` и `import … from …`; проект — `.dsproj`.

| Вид | Корень | Содержимое |
|-----|--------|------------|
| host | `module host <id>` | chrome: `catalog "*.ds"`, `host { language, color_scheme, render, … }`, `surfaces` ([ADR-0054](design/DASHSPEC-ADR-0054-dashhost-module.md)) |
| catalog | `module catalog <id>` | whitelist отчётов: `default`, `entry … dashspec "*.ds"` ([ADR-0023](design/DASHSPEC-ADR-0023-dashcatalog.md)) |
| dashboard | `module dashboard <id>` | `runtime` / `configuration` / `connect` / `report` |
| section | `module section <id>` | вкладка: cards, локальные фильтры ([ADR-0073](design/DASHSPEC-ADR-0073-author-navigation-surface.md)) |
| flow | `module flow <id>` | dataflow-граф: `source`, `transformer`, рёбра |
| diagram | `module diagram <id>` | вид, оси, `scale_y`, series, `render` |
| types | `module types <id>` | строки потоков: `type FixtureRow { optional date … }` |
| palette | `module palette <id>` | `const`-цвета, `default` / `Other` |
| layout | `module layout <id>` | bracket board `[ … ]`, `scope tab` / `scope host` ([ADR-0020](design/DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0021](design/DASHSPEC-ADR-0021-dashlayout-include.md)) |
| tooltip | `module tooltip <id>` | тултипы |
| project | `module project <id>` (`.dsproj`) | `namespace`, `global import` |

### Dashboard (корень отчёта)

`samples/demo/demo-soak.ds` (сокращено):

```text
module dashboard demo_soak
namespace Demo.Soak
import diagrams from Demo.Diagrams
import flows from Demo.Flows as demo_report_flow
import sections from Demo.Analytics

runtime
  manifest = "demo.toml"
end runtime

configuration
  sqldialect = tsql
end configuration

connect
  use palette demo_apps
  use flow demo_report_flow
end connect

report
  title = "DashSpec — Demo soak"

  defaults
    filter.usage_date.range = -7d..today
  end defaults

  filter usage_date
    bind date
      column = usage_date
    end bind
    show
      label = "Report date"
    end show
  end filter

  toolbar chrome
    layout = bar
    sticky = line
    apply = auto
  end chrome

  section overview as "Overview"
    cards
      peak_concurrent_proxy
      events_detail
    end cards
  end section

  card events_detail as "Events detail"
    show flow
      events_top -> [toolbar] chrome.card.events_detail
    end show flow
    data flow
      events_tz [rows] -> [rows] __diagram__
    end data flow
    diagram demo_events_detail_table
      bind
        app_name, user_name
      end bind
  end card
end report
```

- фильтры также объявляются компактной **таблицей** (`| id | bind | column | label | widget | range |` … `end filters`)
- `default -7d..today` — диапазон **в spec**; см. [FILTERS_RU.md](docs/FILTERS_RU.md)
- `bind` на card — фильтры карточки; Core строит `WHERE` / `TOP` ([ADR-0009](design/DASHSPEC-ADR-0009-bind-only-filters.md))

### Section (вкладка в отдельном файле)

`samples/demo/demo-analytics.ds` (сокращено); подключается в dashboard через `import sections from Demo.Analytics`:

```text
module section analytics
namespace Demo.Analytics

connect
  use palette demo_apps
end connect

report
  title = "Analytics"

  card period_peak_by_app as "Peak by app (period)"
    show flow
      period_grain -> [toolbar] chrome.card.period_peak_by_app
    end show flow
    data flow
      period_peak_tz [rows] -> [rows] __diagram__
    end data flow
    diagram demo_period_peak_by_app_bar
      bind
        period_grain, period_start, app_name
      end bind
  end card
end report
```

### Flow (слой данных)

`samples/demo/flows/demo-report.ds` (сокращено; блоки `ports` опущены):

```text
module flow demo_report
namespace Demo.Flows

source daily_peak_raw {
  use provider infer
  from view demo.v_daily_peak_concurrent_proxy
}

transformer daily_peak_tz {
  transform use to_zone { zone = UTC+3 }
}

daily_peak_raw [raw] -> [raw] daily_peak_tz
```

Карточка подключает поток через `data flow` (`daily_peak_tz [rows] -> [rows] __diagram__`).

### Diagram

`samples/demo/diagrams/activity-5min-line.ds`:

```text
module diagram demo_activity_5min_line
namespace Demo.Diagrams

series
  max = 6
  other = "Other"
end series

line
  render = "chartjs"
  x = bucket_start_utc
  x_format = time.short
  x_step = 5m
  y = event_count
  scale_y = integer
  series = app_name
end line
```

### Host и catalog

`samples/demo/demo.ds` + `demo-catalog.ds` (сокращено):

```text
module host demo
import layouts from Demo.Layouts

catalog "demo-catalog.ds"

host
  language = "ru-RU"
  product_title = "DashSpec Demo"
  color_scheme = dark
end host

surfaces
  show help, settings, spec_dev
end surfaces
```

```text
module catalog demo

default demo_soak

entry demo_soak as "DashSpec — Demo soak"
  dashspec "demo-soak.ds"
```

Bootstrap: `[host] dashhost = "../../samples/demo/demo.ds"` в `src/DashSpec.Host/dash-spec.toml`; локальное переопределение — `dash-spec.local.toml` (gitignored). Зритель переключает отчёт в dropdown; автор добавляет `entry` в git.

### Layout (bracket boards)

`samples/demo/layouts/host-topbar.ds` — `scope host`:

```text
module layout demo_host_topbar
namespace Demo.Layouts
scope host

[ brand [] catalog nav external_links help settings ]
```

`scope tab` — та же bracket-модель для раскладки карточек/фильтров вкладки; карточки и фильтры могут нести `ref`-токен для board ([ADR-0022](design/DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md)).

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
- [ADR-0029](design/DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md) — tooltip entity = content; inspect/show = how (accepted)
- [ADR-0043](design/DASHSPEC-ADR-0043-filter-command-palette.md) — `/select` filter commands via CommandPlane (accepted)
- [ADR-0023](design/DASHSPEC-ADR-0023-dashcatalog.md) — `.dashcatalog`
- [ADR-0024](design/DASHSPEC-ADR-0024-document-authoring-layers.md) — document blocks (`runtime`, `configuration`, `wiring`, `body`)
- [ADR-0073](design/DASHSPEC-ADR-0073-author-navigation-surface.md) — sections / author navigation
- [ADR-0101](design/DASHSPEC-ADR-0101-single-unit-file-type.md) — единый тип файла `.ds` (module-модель)

Полный список — каталог [`design/`](design/).

## Тесты

```powershell
dotnet test DashSpec.slnx
```

### Catalog emit drift gate (W0)

Committed `src/DashSpec.Host/Generated/DashCatalog.g.cs` must match `gdlc emit` from `dash.catalog.gdl`. Requires sibling [`authoring-toolchain`](../authoring-toolchain).

```powershell
dotnet msbuild src/DashSpec.Host/DashSpec.Host.csproj -p:Configuration=Release -t:GdlEmitVerify
# or: dotnet test tests/DashSpec.GdlEmit.Tests --filter DashCatalogEmitDrift
```

See [ADR-0052](design/DASHSPEC-ADR-0052-catalog-emit-drift-gate.md).

## Лицензия

Software: [MIT](LICENSE) ([текст OSI](https://opensource.org/license/MIT)) · Ethical use: [declaration](https://github.com/AI-Guiders/licensing/blob/main/docs/ethical-use.md)
