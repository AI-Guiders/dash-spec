# DASHSPEC-ADR-0037: Фильтры — bind, show, видимость и раскладка

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-08 |
| **Relates to** | [ADR-0010](DASHSPEC-ADR-0010-spec-ergonomics.md), [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md), [ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md) |

## Контекст

DSL фильтров наслоился: объявление (`filter …`), видимость (`toolbar`, `page toolbar`, `card filters`), раскладка (`card layout`), привязка к запросу (`bind` на card). Автор видит один и тот же id в нескольких местах и не понимает, что к чему относится.

Термин **placement** использовался неоднозначно:

- список `usage_date, user_name` на toolbar — это **членство и порядок** на панели отчёта, не геометрия относительно диаграммы;
- **`card layout [ filter ] [ diagram ]`** — это **раскладка** виджета фильтра и диаграммы **внутри карточки**.

**Цель:** разделить **что фильтровать**, **как показать**, **где показать в UI** и **как разложить рядом с диаграммой** — по аналогии с `diagram` + `presentation`.

## Решение

### 1. Четыре оси (терминология)

| Ось | Вопрос | Конструкция в spec | IR / runtime |
|-----|--------|-------------------|--------------|
| **bind** (данные) | Что фильтровать? Колонка, default, grain? | `filter` → `bind date` / `bind field` / `bind top` | `FilterDefinition` (kind, column, default, …) |
| **show** (оформление) | Как подписать? Какой виджет? | `filter` → `show` | label, widget, ref ([ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md)) |
| **видимость** | Где в иерархии UI показать виджет? | `toolbar …`, `page … toolbar …`, `card filters …` | `DashboardFilters`, page toolbar, `LocalFilters` |
| **раскладка** | Где **относительно диаграммы** внутри card? | `card layout [ … ] [ … ]` | `InteriorBoard`, `CardInteriorLayoutCompactor` |

Пятая связь — **потребитель запроса** (не путать с `bind` внутри filter):

| Ось | Вопрос | Конструкция |
|-----|--------|-------------|
| **привязка к card** | Какие значения фильтров идут в SQL карточки? | `card … bind usage_date, app_name` |

**Не используем** слово *place* для toolbar-списка. Для геометрии внутри card — только **`layout`** (как в [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md)).

Связь id → объявление: списки видимости (`usage_date, user_name`) — **ссылки по имени** на записи в реестре `filters`, без дублирования column/label/default.

### 2. Объявление filter — только структурный блок

Один id фильтра; два подблока (как `diagram` + `include presentation`):

```text
filter usage_date
  bind date
    column = usage_date
    default = -30d..today
  end bind
  show
    label = "Дата отчёта"
  end show
end filter

filter user_name
  bind field
    column = lus.v_events_detail.user_sam
  end bind
  show
    label = "Пользователь"
    widget = combobox
  end show
end filter

filter events_top
  bind top
    default = 200
  end bind
  show
    label = "Строк (TOP)"
  end show
end filter
```

Правила:

- **`filter <id>`** — ключ состояния сессии. Вид (`date` / `field` / `top`) — в **`bind <kind>`**.
- **`bind`** — только данные: `column`, `default`, `grain_filter`, `min`/`max`, вложенный `labels` для grain.
- **`show`** — только UI: `label`, `widget`, опционально `ref` для board.
- Для field/date блок **`show` обязателен** (label).

Сложный date:

```text
filter period_start
  bind date
    column = period_start
    default = today
    grain_filter = period_grain
    labels
      day = "День"
      month = "Месяц"
      year = "Год"
    end labels
  end bind
  show
    label = "Период"
    widget = day
    ref = period_start
  end show
end filter
```

Lint v1: **`widget`** — в `show`; **default / grain** — в `bind`.

#### Отклонено: однострочный shorthand

```text
filter date usage_date on usage_date label "Дата отчёта" default -30d..today
```

Смешивает bind + show + kind. **Не канон.** Допустим временно как deprecated sugar.

#### Опционально v2: переиспользуемый `show` (как presentation)

```text
filter usage_date
  bind date
    column = usage_date
    default = -30d..today
  end bind
  include show "filters/usage-date.dashshow"
end filter
```

Отложено до появления ≥2 отчётов с одинаковым `show`. v1 — только inline `show`.

### 3. Реестр и видимость

#### `filters` — единственное место объявлений

Все `filter … bind … show …` живут в блоке **`filters`**. Тела filter **не** вкладываются в `toolbar`.

```text
filters
  filter usage_date … end filter
  filter user_name … end filter
  filter app_name … end filter
  filter period_grain … end filter
  filter period_start … end filter
end filters
```

#### `toolbar` — chrome + список ссылок (видимость на панели отчёта)

```text
toolbar
  chrome
    layout = bar
    sticky = line
    apply = auto
    debounce_ms = 400
  end chrome

  usage_date, user_name, app_name
end toolbar
```

- Строка `usage_date, user_name, app_name` — **не данные**, а **какие id из реестра показать** на общей панели и **в каком порядке**.
- Board `[ D P G ]` — тот же смысл, плюс сетка ([ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md)); токены — id или `ref` из `show`.
- Toolbar-фильтры **не привязаны геометрически к диаграмме** — панель над карточками. Связь с chart только через **`bind` на card**.

#### `page … toolbar` — видимость на странице

```text
page peak_util
  toolbar period_grain, period_start, app_name
  …
end page
```

Подмножество реестра для активной страницы ([ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md)). Host пересекает с фильтрами, которые реально `bind` на карточках страницы.

#### `card filters` — видимость на карточке

```text
card events_detail
  filters events_top
  …
end card
```

Filter объявлен в `filters`, но виджет рисуется **на карточке** (типично `top`).

### 4. Раскладка относительно диаграммы — только `card layout`

Когда filter локальный у card, геометрия задаётся **внутри card**:

```text
card events_detail
  filters events_top
  diagram ref events_table lus_events_detail_table
  datasource view lus.v_events_detail
  bind usage_date, user_name, app_name, events_top
  layout
    [ events_top ]
    [ events_table ]
  end layout
end card
```

| Строка board | Слот | Строка сетки |
|--------------|------|--------------|
| `[ events_top ]` | виджет TOP | 1 |
| `[ events_table ]` | диаграмма (diagram ref) | 2 |

`CardInteriorLayoutCompactor` мапит токены на row/col/span **внутри карточки**. Это единственный spatial placement **filter ↔ diagram**.

Без `card layout` локальный filter и diagram — поведение Host по умолчанию (filter сверху, diagram ниже).

### 5. Полный пример (stakeholder)

```text
report
  title = "Отчёты заказчика"

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
    filter app_name … end filter
    filter user_name … end filter
    filter period_grain … end filter
    filter period_start … end filter
  end filters

  toolbar
    chrome … end chrome
    usage_date, user_name, app_name
  end toolbar

  page peak_util
    toolbar period_grain, period_start, app_name
    card stakeholder_peak_by_app
      bind period_grain, period_start, app_name
      diagram …
    end card
  end page
end report
```

| id | bind (данные) | show | видимость | раскладка | SQL card |
|----|---------------|------|-----------|-----------|----------|
| usage_date | date, default | label | toolbar | — | на других page |
| period_grain | field | combobox | page toolbar | — | peak_util cards |
| events_top | top | label | card filters | card layout row 1 | events_detail |

`FilterPlacementAnalyzer`: каждый filter из `bind` card должен иметь валидную **видимость** (toolbar / page toolbar / card filters / host).

### 6. Замены (migration window)

| Было | Стало |
|------|--------|
| `standalone { … }` | `toolbar { chrome …; id, id }` + объявления в `filters` |
| `filter date x on col as "Label" …` | `filter x` + `bind` + `show` |
| `filters dashboard { a, b }` | `toolbar a, b` |
| «placement» в смысле toolbar-списка | **видимость** (список ссылок) |
| «placement» filter у diagram | **`card layout`** |

`standalone` и `as` на filter: warning → error.

### 7. Явно не делаем

- `scope = toolbar` на каждом filter.
- Слияние `bind` card с блоком `bind` внутри filter.
- `.dashfilter` файл в v1.
- Тела filter внутри `toolbar` (только ссылки).

## Последствия

- **Core:** парсер `filter` / `bind` / `show`; `filters` как единый реестр; `toolbar` = chrome + id-list; alias `standalone` → `toolbar`.
- **Host:** без смены модели сессии; `DashboardFilters` = видимость на toolbar; `InteriorBoard` = раскладка в card.
- **LUS:** миграция на `filters` + `toolbar` + структурные filter-блоки.
- **Документация:** не называть toolbar-список «placement относительно диаграммы».

## План миграции

1. Парсер: `bind` / `show`; реестр `filters`; `toolbar` только ссылки.
2. Lint: `show` обязателен для date/field; id в toolbar должен быть в `filters`.
3. Specs и шаблоны; однострочный sugar — deprecated compat.
4. v2: `@show` + `include show`.
