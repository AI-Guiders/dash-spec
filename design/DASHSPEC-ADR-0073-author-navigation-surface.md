# DASHSPEC-ADR-0073: Author navigation surface — `report` → `section` → `page` → `card` → `visual` → `diagram`

| | |
|---|---|
| **Status** | Accepted (grammar + composer: planned; Host: align incrementally) |
| **Date** | 2026-10-01 |
| **Relates to** | [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md), [ADR-0035](DASHSPEC-ADR-0035-chrome-and-filter-widget-families.md), [ADR-0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md), [ADR-0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md), [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md), [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) |

## Context

Продукт изначально задумывался как **декларативный отчёт в git** → Host рисует то, что описано ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)). На практике авторы и агенты сталкиваются с другой моделью:

- **`page`** в DSL (ADR-0030) — по факту **раздел отчёта** (№1, Сводка), а не «лист» внутри раздела;
- **`tab`** — одновременно вкладка soak, тип файла `@tab`, merge-модуль ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) и синтетическая вкладка в рантайме;
- **`phase browse | detail`** — отдельная ось, хотя по смыслу это **листы внутри одного раздела**;
- переключатели в UI используют один визуальный паттерн для разных уровней.

Исполнитель, который **уже знает**, что хочет («отчёт → раздел → лист → KPI/график»), вынужден держать карту платформы. Это не задача автора — признак размытой **навигационной поверхности**.

### Именование: `section`, не `screen`

Ключевое слово верхнего уровня навигации внутри report — **`section`** («раздел отчёта»).

| Кандидат | Почему нет |
|----------|------------|
| `screen` | Ассоциация с монитором / UI-frame, не с содержанием отчёта |
| `tab` | Занято composition / soak ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) |
| `group` | Уже **визуальная** группировка на layout (`group { }` в `.dashlayout`, GroupBox) и **секция каталога** (`group` в `.dashcatalog`, [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md)) |

`section` в author DSL — **навигация отчёта**; не путать с `group` на сетке карточек.

### `widget` — только контрол взаимодействия

В продукте **widget уже занят** ([ADR-0035](DASHSPEC-ADR-0035-chrome-and-filter-widget-families.md), [ADR-0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md)):

```text
filter …
  show
    widget = combobox | day | top | …
  end show
```

Плюс chrome: **Apply** (icon/button), export, fold, view toggles — тоже **widgets** (элементы управления), не аналитика.

**KPI / график / таблица / heatmap** — не widget. Для них в author surface: **`visual`** (смысл); в IR/plugins — **`viz`** / diagram slot ([ADR-0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md), [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md)).

## Decision

### 1. Две плоскости (не смешивать)

**A. Дерево контента (данные → картинка):**

```text
report
  section
    page
      card
        visual
          diagram
```

| Уровень | Смысл | Навигация в UI |
|---------|--------|----------------|
| **report** | Один сценарий в catalog entry | Заголовок / смена entry |
| **section** | Раздел отчёта (№1, №2, Сводка…) | Явный узел в composed tree |
| **page** | Лист внутри раздела (browse / detail) | Явный узел в composed tree |
| **card** | Плитка на layout-сетке: title, interior board, `on click`, `views` | Ячейка сетки |
| **visual** | Один аналитический вывод (scalar KPI, chart, table, matrix, gantt) | Нет |
| **diagram** | Пресет kind + колонок (`@diagram`, `.dashdiagram`) | Нет |

**B. Chrome / ввод (widgets):** слоты toolbar, card-head, filter row — **widget** = `combobox`, `day`, `top`, `button`, `icon`, … Host рисует **только** объявленные widget-слоты ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)); они **не** входят в цепочку `visual → diagram`.

### `card` ≠ `visual`

**card** — контейнер на bracket-board ([ADR-0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md)); внутри **1..N visual**-слотов (`diagram ref …`, `data` / `data for …`, [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md)).

| Сегодня в DSL | Author surface |
|---------------|----------------|
| `view { diagram … }` / `diagram ref main` | **visual** (slot) |
| `show { widget = day }` | **widget** (filter control) |
| `toolbar chrome apply control = icon` | **widget** (chrome) |
| `@diagram` в файле | **diagram** (пресет для visual) |

**Фильтры сценария** — filter **объявления** на page/section; на toolbar они появляются как **widget**-слоты. **visual** только `bind` к filter; не объявляет filter UI.

### 2. Соответствие demo stakeholder (пример)

Четыре сценария заказчика — **четыре `section`** в одном `report` (см. §1). `phase browse | detail` → **`page`** под одним `section`. Legacy top-level `page peak_util` → **`section peak_util`** + `page main`.

### 3. Что уходит с author surface

| Конструкция | Судьба |
|-------------|--------|
| `@tab` как тип корня файла | **Composition**: import / catalog / host bundle |
| `tab … dashspec` в parent | Merge в composer |
| `standalone { }` | Composer: embed vs entry |
| `page` (ADR-0030, top-level) | **Migrate** → `section` |
| `phase` | **Migrate** → `page` |

### 4. Grammar (целевой скелет)

```text
report "Сводка"
  navigation
    initial section = peak_util
    initial page = main
  end navigation

  filters
    filter period_start
      show
        label = "Период"
        widget = day
      end show
    end filter
  end filters

  section peak_util
    page main
      toolbar period_grain, period_start, app_name, chart_top
      include layout "…"
      card kpi_over_limit
        visual main
          diagram demo_stakeholder_kpi_over_limit
          data … bind … end data
        end visual
      end card
    end page
  end section
end report
```

Keyword **`visual`** может вводиться как обёртка над `diagram ref` + `data`; до P1 DSL остаётся `view` / `diagram ref`, composer маппит в author tree.

### 5. Анализатор (author guardrails)

- `bind` на filter, не объявленный на report / section / page → **error**.
- Тот же filter на visual-local / card-local и на page/section → **error**.
- `navigation.show = hidden` — только из spec/composer, не из Host.

## Relationship to prior ADRs

- [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md): top-level `page` → `section`; browse/detail → nested `page`.
- [ADR-0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md): **widget** = filter/chrome control only.
- [ADR-0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md): рендер **visual** через viz plugins.
- [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md): page toolbar slots = **widgets**; card interior = **visual** slots + optional filter **widgets**.

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | ADR-0073 + ADR-0074; author guide |
| **P1** | Composer: section/page + navigation; visual slot naming |
| **P2** | Migrate demo; phase → page |
| **P3** | Portal soak; drop synthetic tab nav |

## Consequences

- «Виджет» в разговоре про кнопку/фильтр = **widget**; про KPI/график = **visual**.
- Alias + codemod; Host через composed view ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)).
