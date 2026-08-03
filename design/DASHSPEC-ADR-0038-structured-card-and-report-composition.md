# DASHSPEC-ADR-0038: Структурированный card и report как composition

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-07-08 |
| **Relates to** | [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0037](DASHSPEC-ADR-0037-filter-scopes-and-toolbar-grouping.md) |

## Контекст

Обсуждение цельности синтаксиса выявило разрыв: реестр, сцена UI и поведение читаются как три несвязанных слоя. Предлагались блоки `definitions { }`, `composition { }` и keyword `panel` как замена `card`.

**Отклонено:**

- корневой `definitions { }` / `composition { }` — лишние контейнеры;
- keyword **`panel`** — не несёт новой семантики для Host; дублирует `card`.

**Принято:** цельность через **структуру внутри `card`** (`data` / `view` / `layout`) и роль **`report`** как сцены. IR и Host — без изменений (`CardDefinition`).

## Решение

### 1. Три слоя модуля (без новых корней)

| Слой | Уже есть | Роль |
|------|----------|------|
| **Конверт** | `runtime`, `configuration`, `wiring`, `!include` | инфра, diagram/layout как файлы |
| **Реестр filter** | `filters` | объявления filter ([ADR-0037](DASHSPEC-ADR-0037-filter-scopes-and-toolbar-grouping.md)) |
| **Сцена** | `report` | toolbar, pages, cards |

`report` **и есть** composition. Отдельный keyword не нужен.

### 2. Структурированный `card`

Канонический card — **вложенные подблоки**, не плоский список директив.

```text
card stakeholder_peak_by_app
  title = "Пик одновременности по ПО"

  data
    datasource view lus.v_peak_concurrent_by_period
    bind period_grain, period_start, app_name
  end data

  view
    diagram lus_stakeholder_peak_by_app_bar
  end view

  layout
    place
      row = 1
      col = 1
      span = 8
    end place
  end layout

  chrome
    bound_filters = hidden
  end chrome
end card
```

| Подблок | Слой (как у filter) | Содержимое |
|---------|---------------------|------------|
| **data** | bind (данные) | `datasource`, `bind` (id из `filters`) |
| **view** | ссылка на chart | `diagram` (id); `legend` — если не в diagram |
| **layout** | геометрия | `place` на page grid; bracket board для filter↔diagram внутри card |
| **chrome** | оболочка | `bound_filters`, extension blocks (`views`, …) |

**Не в `view`:** `transform series` — параметр **diagram** (блок `series` в `.dashdiagram`), см. §2.1.

**Обязательны в каноне:** `data`, `view`.  
**Опциональны:** `layout`, `chrome`.

#### 2.1. `series` — в diagram, не на card

Лимит серий (`max`, `other`) — свойство **вида графика** (bar/line с `series = app_name`), не card и не SQL.

**Канон** — в файле `@diagram`:

```text
@diagram lus_stakeholder_peak_by_app_bar
  include presentation "../../presentations/bar-horizontal-320.dashpresentation"

  series
    max = 12
  end series

  bind bar
    category = app_name
    value = peak_concurrent_proxy
    reference = purchased_seats
    order_by = "peak_concurrent_proxy DESC, app_name"
  end bind
  show
    render = chartjs
    orientation = horizontal
    scale_value = integer
  end show
end diagram
```

На card в `view` — **только ссылка**:

```text
view
  diagram lus_stakeholder_peak_by_app_bar
end view
```

| Место | Роль |
|-------|------|
| **diagram `series`** | дефолт для всех card с этим chart |
| **card `transform series`** (legacy) | редкий override; **deprecated** → **`override for <diagram_id>`** (§2.2) |

Миграция LUS: перенести `transform series max = N` с card в `.dashdiagram`; если max разный per card — `override for <id>`.

#### 2.2. Override diagram на card — `for <diagram_id>`

Дефолт живёт в **diagram file** (и presentation presets). Card меняет только дельту — с **явным id** цели (как ADR-0008: `diagram <id> { … }`).

**«Для кого»** = **id diagram preset / include**, не имя фрагмента (`series`, `legend` — это *что* меняем внутри).

Два равноправных способа:

**A. Inline у ссылки** (дельта рядом с `diagram`):

```text
view
  diagram lus_stakeholder_peak_by_app_bar
    series
      max = 12
    end series
  end diagram
end view
```

**B. Отдельный блок `overrides`** (все дельты в одном месте; удобно, когда `view` только ссылки):

```text
view
  diagram lus_stakeholder_peak_by_app_bar
end view

overrides
  for lus_stakeholder_peak_by_app_bar
    series
      max = 12
    end series
  end for
end overrides
```

Синоним одной цели (если override один) — **одно поле inline**, без лишнего `end series`:

```text
override for lus_stakeholder_peak_by_app_bar
  series max = 12
end override
```

Несколько полей — вложенный блок `series` + **один** `end series`, затем `end override`:

```text
override for lus_stakeholder_peak_by_app_bar
  series
    max = 12
    other = "Прочие"
  end series
end override
```

Закрытие: `end series` — только если открывали многострочный `series`; снаружи всегда `end override` / `end for` / `end overrides`, не второй `end series`.

| Синтаксис | Когда |
|-----------|--------|
| **`diagram <id>` + body** | 1–2 поля; дельта видна сразу у ссылки |
| **`overrides` / `override for <id>`** | несколько полей или несколько diagram на card |
| **`transform series` на card** (legacy) | deprecated → `override for <id>` |

Внутри `for <id>` — те же подблоки, что в diagram file:

| Подблок | Пример |
|---------|--------|
| **`series`** | `max`, `other` |
| **`legend`** | `min`, `max` |
| **`show`** | `height`, `orientation` |
| **bind-поля** | `y = other_column` (редко) |

Правила:

- **`for <id>`** должен совпадать с diagram, на который ссылается `view` (lint).
- При `diagram ref <slot> <preset_id>` в **`for`** — **preset id** (`lus_events_detail_table`), не slot.
- Merge: **diagram file → inline `diagram <id>` body → `override for <id>`** (ADR-0008).
- **`view`** без override — только `diagram <id>` (чистая ссылка).

**Не путать:**

| Блок | Роль |
|------|------|
| **`data.bind`** | какие **filter** в SQL |
| **`override for <diagram_id>`** | дельта **конкретного** chart preset |
| **`filters events_top`** | видимость filter на card |

Поведение — siblings внутри `card`: `when`, `on click`, `filters` (видимость локальных filter), `limits`, **`overrides` / `override for <id>`** (дельта diagram).

#### Card-local filter + interior layout

```text
card events_detail
  filters events_top

  data
    datasource view lus.v_events_detail
    bind usage_date, user_name, app_name, events_top
  end data

  view
    diagram ref events_table lus_events_detail_table
  end view

  layout
    [ events_top ]
    [ events_table ]
  end layout
end card
```

- `filters events_top` — **видимость** (виджет на card).
- `layout [ events_top ] [ events_table ]` — **геометрия** filter над diagram.

#### Legacy: плоский card

```text
card x
  diagram …
  datasource …
  bind …
end card
```

Парсится в migration window; lint **deprecated** → error. Desugar в тот же `CardDefinition`.

**Не вводим** keyword `panel`.

### 3. Симметрия слоёв (filter · diagram · card)

| Сущность | Данные | Оформление | Shaping / прочее | Геометрия / видимость |
|----------|--------|------------|------------------|------------------------|
| **filter** | `bind` в `filters` | `show` | — | `toolbar` / `page toolbar` / `card filters` |
| **diagram** | `bind` (оси, category, value) | `show` (+ presentation) | **`series`** (max, other) | — |
| **card** | `data` | `view` → ссылка на diagram | — | `layout`; **`override for <id>`** |

Один паттерн: **что → как выглядит → (для chart) лимит серий → где на сцене**.

### 4. Полный пример (stakeholder)

```text
@tab stakeholder

runtime … end runtime
configuration … end configuration
!include "diagrams/stakeholder/*.dashdiagram"

wiring
  use connector sqlserver
  use palette lus_apps
  layout grid
    columns = 12
    gap = 16
  end grid
end wiring

filters
  filter usage_date
    bind date
      column = usage_date
      default = -30d..today
    end bind
    show
      label = "Дата отчёта"
    end show
  end filter
  filter period_grain … end filter
  filter period_start … end filter
end filters

report
  title = "Отчёты заказчика"

  toolbar
    chrome … end chrome
    usage_date, user_name, app_name
  end toolbar

  page peak_util
    title = "№1 Закупка и утилизация"
    include layout "layouts/stakeholder-page-peak-util.dashlayout"
    toolbar period_grain, period_start, app_name

    card stakeholder_peak_by_app
      title = "Пик одновременности по ПО"
      data … end data
      view … end view
      layout … end layout
      chrome … end chrome
    end card

    card stakeholder_utilization
      …
    end card
  end page

  page multi_app
    derive usage_date from period_start grain period_grain
    phase browse
      card stakeholder_peak_apps_browse
        when user_name empty
        on click … end click
        data … end data
        view … end view
      end card
    end phase
  end page
end report
```

### 5. Миграция

| Было | Стало |
|------|--------|
| `card` + плоские `diagram` / `bind` / `datasource` | `card` + `data` / `view` / `layout` |
| `transform series` на card | `series` в diagram; исключения → **`override for <diagram_id>`** |
| `standalone` | `toolbar` ([ADR-0037](DASHSPEC-ADR-0037-filter-scopes-and-toolbar-grouping.md)) |
| `panel` (черновик ADR) | **не вводим** |

Парсер: structured card → существующий `CardDefinition`; flat card — compat.

### 6. Явно не делаем

- `definitions { }`, `composition { }`, `panel`
- `scope =` на сущностях
- Новый IR-тип вместо `CardDefinition`

## Последствия

- **Core:** `CardParser` — подблоки `data`/`view`/`layout`; flat body deprecated.
- **Host:** без изменений.
- **LUS:** миграция specs на structured card по мере правок.
- **ADR-0037:** реестр filter в `filters`; сцена в `report`.

## Открытые вопросы

- Строгий порядок подблоков в card (`data` → `view` → `layout` → `chrome`) или любой
- `legend` в diagram `show` vs отдельно на card `view`
- `bind` / `show` в `.dashdiagram` — миграция с плоского `bar { }` (отдельный этап)
- Имя блока в diagram: `series` (не `transform series`)
- Цель override: **`for <diagram_id>`** (или inline body у `diagram <id>`)
- Внутри: именованные фрагменты (`series`, `legend`, `show`, bind-поля)
