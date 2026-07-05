# DASHSPEC-ADR-0024: Document grammar — blocks, `.dashinclude`, `report`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md), [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md) |

## Context

В одном `.dashspec` смешаны manifest, includes, wiring и report DSL без синтаксической границы. Flat `@runtime`, `include`, `connector`, голые `card` — **снимаем**; один канонический скелет блоков.

## Decision

### Модульный корень: `@kind id { }`

**`@start` / `@end` не вводим.** `@` — только **корень файла** (тип + id). Содержимое — keyword-блоки в `{ }`.

```text
@tab stakeholder {
  runtime { manifest = "lus-runtime.toml" }
  configuration { sqldialect = tsql palette = "palettes/lus-apps.dashpalette" }
  !include "imports/stakeholder.dashinclude"
  wiring { use connector sqlserver use palette lus_apps }
  report { … }
}
```

| Конструкция | Уровень | Смысл |
|-------------|---------|--------|
| `@tab id { … }` | файл | tab module ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) |
| `@dashboard id { … }` | файл | root dashboard |
| `tab id as "…" dashspec "…"` | внутри `report` parent | ссылка на tab module |
| `tab id as "…" { cards { … } }` | внутри `report` parent | inline tab (card list) |

Inner `tab id as "…" { filter … }` **внутри tab module** — **удалён** → `standalone { }` + `filters { }`.

### Скелет module `{ }`

```text
@<moduleKind> <id> {
  runtime { … }
  configuration { … }
  !include "…/*.dashinclude"     # опционально, повторяемо
  wiring { … }
  report ["Title"] { … }
}
```

Порядок секций **strict** — иначе parse error.

### `title`

Optional string только на **`report "Title" { }`** (root dashboard / standalone tab entry). Иначе title из `entry … as "Title"` в catalog ([ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md)).

### Layer 0: `runtime` / `configuration`

```text
runtime {
  manifest = "lus-runtime.toml"
}

configuration {
  sqldialect = tsql
  palette = "palettes/lus-apps.dashpalette"
}
```

Secrets и connection strings — **только** TOML из `manifest` ([ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md)).

### Layer 2: includes — `.dashinclude` и glob

**Explicit bundle** (layout + named diagrams, порядок важен):

```text
@include stakeholder_shell

layout "layouts/stakeholder-grid.dashlayout"
diagram "diagrams/stakeholder-peak-apps-heatmap.dashdiagram"
```

**Glob в module** — без ручного registry на каждый файл:

```text
@tab stakeholder {
  ...
  !include "layouts/stakeholder-grid.dashlayout"
  !include "diagrams/stakeholder/*.dashdiagram"
  wiring { ... }
  report { ... }
}
```

| `!include` аргумент | Резолв |
|---------------------|--------|
| `"imports/stakeholder.dashinclude"` | один bundle → строки `layout` / `diagram` |
| `"diagrams/stakeholder/*.dashdiagram"` | все `.dashdiagram` в каталоге |
| `"layouts/*.dashlayout"` | все layout-модули (редко; обычно один board на tab) |
| `"imports/*.dashinclude"` | несколько bundle, merge по sorted path |

Правила glob (v1):

- База путей — каталог **корневого** `.dashspec` (как у relative include в [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)).
- `*` — один уровень каталога (не recursive). `**` — follow-up при необходимости.
- Порядок expand — **lexicographic по full path** (детерминированный IR, воспроизводимые diff).
- Пустой glob → **warning** в lint; missing diagram на card → **error** при resolve.
- Duplicate `@diagram` id после expand → **error** (как без glob).
- Glob **явный** в `!include`; auto-discovery по всему репо без директивы — **не делаем**.

Можно **комбинировать**: explicit `.dashinclude` + glob diagrams; explicit layout + glob diagrams. Hybrid profile часто: spec цельный, `!include "diagrams/<tab>/*.dashdiagram"`.

В `.dashspec` секция `!include` — **повторяемая**, порядок строк = порядок merge registry.

### Layer 3: `wiring`

```text
wiring {
  use connector sqlserver
  use palette lus_apps
  layout grid { columns = 12 gap = 16 }
}
```

### Layer 4: `report { }`

#### Root `@dashboard`

```text
@dashboard lus_dev_soak {
  runtime { manifest = "lus-runtime.toml" }
  configuration { sqldialect = tsql palette = "palettes/lus-apps.dashpalette" }
  !include "imports/soak.dashinclude"
  wiring { use connector sqlserver use palette lus_apps }

  report "License Usage — Dev Soak" {
    filter date usage_date on usage_date as "Дата отчёта" default -7d..today
    toolbar chrome { layout = bar apply = auto }
    toolbar { usage_date user_name }

    tab stakeholder as "Отчёты заказчика" dashspec "lus-dev-stakeholder.dashspec"
  }
}
```

`filter` / `toolbar` / `tab … dashspec` — **напрямую** в `report` (без `standalone` / `filters`).

#### Tab module `@tab`

```text
@tab stakeholder {
  runtime { manifest = "lus-runtime.toml" }
  configuration { sqldialect = tsql palette = "palettes/lus-apps.dashpalette" }
  !include "imports/stakeholder.dashinclude"
  wiring { use connector sqlserver use palette lus_apps }

  report {
    standalone {
      filter date usage_date on usage_date as "Дата отчёта" default -7d..today
      toolbar chrome { layout = bar apply = auto }
      toolbar { usage_date user_name app_name }
    }

    filters {
      filter field period_grain on … as "Масштаб" ref G default day
      filter date period_start on period_start as "Период" ref P … grain_filter period_grain
    }

    card stakeholder_peak_apps as "№2 …" ref E {
      diagram lus_stakeholder_peak_apps_heatmap
      datasource view lus.v_daily_peak_concurrent_apps_per_user
      bind usage_date, user_name
    }
  }
}
```

| Блок в tab `report` | Standalone | Embed |
|---------------------|------------|-------|
| `standalone { }` | filters + toolbar модуля | ignore |
| `filters { }` | + к filter set | merge в parent |
| `card` | cards вкладки | merge |

Embed-minimal: `@tab id { report { filters { … } card … } } }`.

#### Card — diagram: inline или по id

**Modular** (product, много charts, reuse):

```text
card … {
  diagram lus_stakeholder_peak_apps_heatmap   # id из .dashdiagram, registry в .dashinclude
  datasource view …
  bind …
}
```

**Monolith** (прототип, один файл — см. ниже):

```text
card … {
  diagram heatmap {
    x = usage_date as "День"
    y = user_sam as "Пользователь"
    value = peak_concurrent_apps as "Разных ПО"
    tooltip = peak_apps as "Состав в пике"
    tooltip_format = list
  }
  datasource view …
  bind …
}
```

На card — **либо** `diagram <id>`, **либо** inline `diagram <kind> { … }`, не оба. Parse → один IR.

#### `.dashdiagram` — тело файла без второго `diagram`

На **card** слово `diagram` — имя **слота** рядом с `datasource` / `bind`. В **файле** корень `@diagram <id>` уже задаёт тип и registry id — повторять `diagram` странно (в отличие от `@layout`, где тело сразу `[ Q W ]`).

**Канон** (как `@layout` — id на первой строке, тело без обёртки `{ }`):

```text
@diagram lus_stakeholder_peak_apps_heatmap

!include "<presentation/heatmap_tall>"

heatmap {
  x = usage_date as "День"
  y = user_sam as "Пользователь"
  value = peak_concurrent_apps as "Разных ПО"
  tooltip = peak_apps as "Состав в пике"
  tooltip_format = list
}
```

| Контекст | Синтаксис | Зачем `diagram` |
|----------|-----------|-----------------|
| **card** | `diagram heatmap { … }` или `diagram <id>` | слот среди bind/datasource |
| **`.dashdiagram`** | `heatmap { … }` (+ optional `!include`, `presentation`, `transform series`) | не нужен — `@diagram id` + расширение файла |
| **`.dashlayout`** | `[ Q W ]` строки | аналог: `@layout id`, без inner `layout` |

Один **`ParseKindBlock(kind)`** (heatmap/bar/line + properties) вызывается:
- после keyword `diagram` на card;
- напрямую как top-level statement в теле `@diagram` file.

`@diagram id { diagram heatmap { } }` — **удалён** (лишняя вложенность). Extract monolith → file: id на `@diagram`, kind-block без prefix.

#### Уникальность `@diagram` id

Scope = один resolved dashboard. Duplicate id → **error**, last-wins запрещён. Prefix convention: `lus_stakeholder_peak_apps_heatmap`.

**Monolith:** inline diagram на card **без** `@diagram` id — коллизий registry нет. При extract в файл автор задаёт id.

### Authoring profiles: monolith vs modular (+ glob)

Один **блочный** DSL; способы **упаковки по файлам**. Parser → один IR.

| | **Monolith** | **Hybrid** | **Modular** |
|---|--------------|------------|-------------|
| Когда | прототип, 1–3 card | product tab, diff spec | reuse diagram между tab |
| Файлов | 1× `.dashspec` | spec + `diagrams/<tab>/*` | + `.dashinclude` при нужде |
| Diagram | inline на card | `diagram <id>` + glob | bundle или glob |
| Layout | inline board | explicit `.dashlayout` | `.dashinclude` или explicit |
| Includes | — | `!include "diagrams/stakeholder/*.dashdiagram"` | bundle и/или glob |

**Monolith example:** `URSA.LicenseUsage/docs/dashspec/templates/tab-module-monolith.dashspec.template`.

**Modular / hybrid convention (LUS):**

```text
docs/dashspec/
  diagrams/<tab>/*.dashdiagram     # glob в !include
  layouts/*.dashlayout
  imports/<tab>.dashinclude        # опционально: toolbar, explicit order
  lus-dev-*.dashspec
```

**Extract (monolith → hybrid):** inline diagram → `diagrams/<tab>/<id>.dashdiagram`; на card `diagram <id>`; в module одна строка glob вместо N строк registry.

`runtime.toml` с secrets — **всегда** отдельно, в обоих profile.

### Fragment files vs module files

Два класса файлов — разный синтаксис корня:

| Класс | Файлы | Корень | Тело |
|-------|-------|--------|------|
| **Module** | `.dashspec` | `@dashboard id { }` / `@tab id { }` | named-секции: `runtime`, `wiring`, `report`, … |
| **Fragment** | `.dashdiagram`, `.dashlayout`, `.dashpresentation`, `.dashpalette`, `.dashtransform`, `.dashcatalog` | `@kind id` (без outer `{ }`) | см. таблицу ниже |
| **Registry** | `.dashinclude` | `@include id` | строки registry / `!include` |

**Правило:** inner keyword **= имя файла/корня** → дубль, убираем. Inner keyword **= слот card/diagram-module** (рядом с другими слоями) → оставляем.

#### Fragment canon (без inner duplicate)

| Файл | Было (дубль) | Канон |
|------|--------------|-------|
| `.dashlayout` | — | `@layout id` → строки `[ Q W ]` ✓ уже ок |
| `.dashcatalog` | — | `@catalog id` → `default`, `entry … dashspec` ✓ уже ок |
| `.dashdiagram` | `diagram heatmap { }` | `heatmap { }` (+ слои ниже) |
| `.dashpresentation` | `presentation { legend = bottom }` | `@presentation id` → properties напрямую |
| `.dashpalette` | `palette { Tekla = tekla }` | `@palette id` → `const …` + mappings напрямую |
| `.dashtransform` | `transform series { max = 5 }` | `@transform id` → properties напрямую (top-level `series` block — follow-up) |

**`.dashpresentation` — канон:**

```text
@presentation bar_horizontal_320

legend = bottom
height = 320
```

**`.dashpalette` — канон:**

```text
@palette lus_apps

const tekla = "#e11d48"
default = default
Tekla = tekla
colors = [tekla, aveva, cursor]
```

**`.dashtransform` — канон:**

```text
@transform top5

max = 5
other = "Other"
```

#### Слои внутри `.dashdiagram` (не дубли)

Тело `@diagram` — **mini-card** без datasource/bind. Слоты как на card:

```text
@diagram lus_peak_concurrent_line

!include "../presentations/line-bottom-300.dashpresentation"

series {
  max = 5
  other = "Other"
}

line {
  x = usage_date as "День"
  y = peak_concurrent_proxy as "Пик"
  series = app_name
}
```

| В diagram-module | На card | Комментарий |
|------------------|---------|-------------|
| `heatmap { }` / `line { }` | `diagram heatmap { }` | на card нужен slot `diagram` |
| `presentation { height = 420 }` | `presentation { … }` | inline слой; ref → `!include` |
| `series { max = 5 }` | `transform series { … }` | в файле контекст transform; `transform` prefix опционален |
| `!include "…"` | — | unified include (legacy `include presentation` → `!include`) |

#### `.dashinclude` — mild duplicate

`@include id` + расширение `.dashinclude` — label bundle, терпимо. Строки `layout "…"` / `diagram "…"` дублируют тип файла по extension → **follow-up:** только `!include "path"` (тип из расширения). С glob на module `.dashinclude` часто не нужен.

#### Legacy в live `.dashspec` (не fragment, но дубли)

| Было | Замена |
|------|--------|
| `@tab stakeholder` + `tab stakeholder { filter … }` | `@tab { report { standalone filters card } }` |
| `@runtime "…"`, `connector`, `include layout` | `runtime { }`, `wiring { }`, `!include` |

### `.dashcatalog`

```text
@catalog lus_dev

default soak

entry soak as "License Usage — Dev Soak"
  dashspec "lus-dev-soak.dashspec"
```

### Типы файлов

| Расширение | Корень | Тело (канон) |
|------------|--------|--------------|
| `.dashspec` | `@dashboard id { }` / `@tab id { }` | module-секции |
| `.dashinclude` | `@include id` | registry / `!include` paths |
| `.dashdiagram` | `@diagram id` | `!include`, `series { }`, `<kind> { }` |
| `.dashlayout` | `@layout id` | `[ Q W ]` rows |
| `.dashpresentation` | `@presentation id` | properties (без `presentation { }`) |
| `.dashpalette` | `@palette id` | `const` + mappings (без `palette { }`) |
| `.dashtransform` | `@transform id` | properties (без `transform series { }`) |
| `.dashcatalog` | `@catalog id` | `default`, `entry …` |

### Удалённый синтаксис

| Было | Замена |
|------|--------|
| `presentation { … }` в `.dashpresentation` | properties напрямую под `@presentation id` |
| `palette { … }` в `.dashpalette` | mappings напрямую под `@palette id` |
| `transform series { … }` в `.dashtransform` | properties напрямую под `@transform id` |
| `diagram kind { … }` в `.dashdiagram` | `<kind> { … }` |
| `include presentation` в diagram | `!include "path.dashpresentation"` |
| `@runtime "…"`, flat `@sqldialect` | `runtime { }`, `configuration { }` |
| `include` / card `include diagram` | inline `diagram kind { }` на card или `diagram <id>` + `.dashdiagram` (`@diagram id` + `kind { }`) |
| `connector` / `palette` | `wiring { use … }` |
| `dashboard "T" { }` | `@dashboard id { report "T" { } }` |
| inner `tab id { filter }` в module | `filters { }` / `standalone { }` |
| `@tab id` без `{ }` | `@tab id { … }` |

## Оценка подхода

**За:** один визуальный скелет; слои manifest / wiring / report читаются без ADR; dual standalone/embed tab module явно через `standalone` vs `filters`; тот же `{ }` паттерн, что у `runtime`, `wiring`, `catalog`.

**Риски:** больше вложенности; strict order секций; modular — много файлов без convention.

**Вывод:** блочный DSL **обязателен**; split по файлам — **опционален** (monolith vs modular). Магия «что куда» снимается convention + extract path, не отказом от блоков.

### Parser model: блок = документ

Блочная грамматика выбрана не только для читаемости — она **упрощает и расширяет parser**.

**Принцип:** файл — это **контейнер одного `@kind id { … }` корня**; тело `{ … }` — **набор named-блоков** того же вида, что могли бы жить inline. Парсер не различает «уровень файла» и «уровень блока» — один и тот же **block dispatcher**.

```text
ParseDocument(text)
  → root = ReadRoot()                    # @dashboard | @tab | @diagram | …
  → body  = ParseBlockBody(root.Kind)    # keyword { } до закрытия корня

ParseBlockBody(kind):
  loop keyword:
    runtime      → ParseRuntimeBlock()
    configuration→ ParseConfigurationBlock()
    !include     → ExpandIncludes() → ParseDocument(each)  # рекурсия, тот же pipeline
    wiring       → ParseWiringBlock()
    report       → ParseReportBlock()
    diagram      → ParseKindBlock()         # на card — после keyword; в @diagram file — top-level kind
    …
```

| Концепт | Файл | Inline-блок | Parser |
|---------|------|-------------|--------|
| Tab module | `@tab id { … }` в `.dashspec` | — | `ParseBlockBody(TabModule)` |
| Diagram | `@diagram id` → `heatmap { }` | `card { diagram heatmap { } }` | `ParseKindBlock()` |
| Layout | `@layout id` → `[ Q W ]` | `wiring { layout board { … } }` | `ParseLayoutBoard()` |
| Presentation | `@presentation id` → props | `card` / diagram-module: `presentation { }` | `ParsePresentationProps()` |
| Palette | `@palette id` → const + map | `configuration.palette` path + `wiring { use palette }` | ref, не inline body |
| Registry | `.dashinclude` строки | — | expand → `ParseDocument` per path |

**Следствия:**

1. **Расширяемость** — новый слой = новый keyword-блок + handler в `ParseBlockBody`; не новый «режим файла» и не flat-directive ветка.
2. **`!include` / glob** — preprocess: path → text → **тот же** `ParseDocument`; merge registry по правилам kind, не отдельный include-grammar.
3. **Monolith ↔ modular** — один IR; extract = cut `@diagram` block в файл, card меняет inline → ref. Parser не меняется.
4. **Multi-root file (follow-up)** — несколько `@kind id { }` подряд = несколько вызовов `ParseDocument` на одном stream.
5. **Legacy упрощается удалением** — flat `@runtime`, `connector`, inner `tab { filter }` были отдельными code path; блоки их **схлопывают**.

**Целевая структура Core (вместо разрозненных shell/tab modes):**

```text
DocumentSectionsParser     # корень + strict order секций module
BlockParsers/                # runtime, configuration, wiring, report, …
FragmentParsers/             # diagram, layout, palette — переиспользуются из report и из @-file
IncludeExpander              # !include, glob → List<ParseDocument>
DashboardComposer            # compose root + tab refs + embed merge (без parse-logic)
```

Текущий `DashboardShellParser` + `TabModuleParser` + flat directives — **transitional**; миграция на block dispatcher — часть implementation plan ниже.

## Follow-up

| Тема | Статус |
|------|--------|
| single declaration, layout by id; deprecate `ref` | [ADR-0027](DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md) (proposed) |
| inspect (tooltip) vs diagram bindings | [ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md) (proposed) |
| bounded `on click` (show/set/goto) | [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) |
| `on click` drill-down polish | follow-up |
| `dashspec lint` / extract helper | follow-up |

## Implementation plan

1. **`DocumentSectionsParser` + block dispatcher** — корень `@kind id`, strict order; `ParseBlockBody` общий для file и inline; замена `DashboardShellMode` веток.
2. **`FragmentParsers`** — `diagram`, `layout`, `palette`: один parser на card inline и на `@`-file.
3. **`IncludeExpander`** — `!include` + glob → `ParseDocument` per match; merge registry.
4. **`report` blocks** — `standalone`, `filters`, `card`; embed merge в composer.
5. `@catalog id { }`; templates monolith + hybrid (glob).

## Consequences

- Tab module: `@tab { report { standalone, filters, card } }` — без inner `tab`.
- Breaking 0.x; templates — канон.
