# DASHSPEC-ADR-0073: Author navigation surface — `report` → `section` → `page` → `card` → `widget` → `diagram`

| | |
|---|---|
| **Status** | Accepted (grammar + composer: planned; Host: align incrementally) |
| **Date** | 2026-10-01 |
| **Relates to** | [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md), [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) |

## Context

Продукт изначально задумывался как **декларативный отчёт в git** → Host рисует то, что описано ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)). На практике авторы и агенты сталкиваются с другой моделью:

- **`page`** в DSL (ADR-0030) — по факту **раздел отчёта** (№1, Сводка), а не «лист» внутри раздела;
- **`tab`** — одновременно вкладка soak, тип файла `@tab`, merge-модуль ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) и синтетическая вкладка в рантайме;
- **`phase browse | detail`** — отдельная ось, хотя по смыслу это **листы внутри одного раздела**;
- переключатели в UI используют один визуальный паттерн для разных уровней.

Исполнитель, который **уже знает**, что хочет («отчёт → раздел → лист → виджет»), вынужден держать карту платформы. Это не задача автора — признак размытой **навигационной поверхности**.

### Именование: `section`, не `screen`

Ключевое слово верхнего уровня навигации внутри report — **`section`** («раздел отчёта»).

| Кандидат | Почему нет |
|----------|------------|
| `screen` | Ассоциация с монитором / UI-frame, не с содержанием отчёта |
| `tab` | Занято composition / soak ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) |
| `group` | Уже **визуальная** группировка на layout (`group { }` в `.dashlayout`, GroupBox) и **секция каталога** (`group` в `.dashcatalog`, [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md)) |

`section` в author DSL — **навигация отчёта**; не путать с `group` на сетке карточек.

## Decision

### 1. Каноническая иерархия (author + agent)

```text
report
  section
    page
      card
        widget
          diagram
```

| Уровень | Смысл | Навигация в UI |
|---------|--------|----------------|
| **report** | Один сценарий в catalog entry | Заголовок / смена entry |
| **section** | Раздел отчёта (№1, №2, Сводка…) | Явный узел в composed tree |
| **page** | Лист внутри раздела (browse / detail) | Явный узел в composed tree |
| **card** | **Плитка на layout-сетке**: заголовок, chrome, export, interior board, `on click`, `views` | Нет (ячейка сетки) |
| **widget** | **Один визуал**: KPI, график, таблица, heatmap ([ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md) — несколько widget в одном card) | Нет |
| **diagram** | Декларация kind + колонок (часто `!include` `.dashdiagram`) | Нет |

### `card` ≠ виджет

В BI пользователь говорит «виджет / визуал» про **KPI или диаграмму**, не про рамку с кнопкой Export. В DashSpec **`card`** — **контейнер** на bracket-board страницы ([ADR-0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md), [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md)): может содержать **0..N** widget-слотов (`diagram ref …`, `data` / `data for …`).

| Сегодня в DSL | Author surface (цель) |
|---------------|------------------------|
| `card` + `view { diagram … }` | `card` + один или несколько **`widget`** |
| `diagram ref main` / `diagram ref drill` | отдельные **widget** в interior card |
| `@diagram` в файле | **`diagram`** — пресет для widget |

Слово **widget** в toolbar filter UI ([ADR-0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md)) — **контрол фильтра**, не report widget; в гайде: «filter control» vs «report widget (визуал)».

**Фильтры сценария** — на **page** (минимум) или на **section**, если общие для всех page внутри. **widget** только `bind` к объявленным filter; глобальные filter не на widget. Локальные filter на **card** (interior row) — явно, без Host-умолчаний ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)).

### 2. Соответствие demo stakeholder (пример)

Четыре сценария заказчика — **четыре `section`** в одном `report`:

```text
report stakeholder
  section peak_util
    page main
      card …
  section multi_app
    page browse
      card …
    page detail
      card …
  section idle
    page main
      card …
  section executive_summary
    page main
      card …
```

- `phase browse | detail` → **`page browse` / `page detail`** под одним `section`.
- Текущий `page peak_util` (ADR-0030) → **`section peak_util`** + `page main` (или несколько page).

Разбиение №1–Сводка на section vs page — **автор в спеке**; Host не схлопывает уровни ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)).

### 3. Что уходит с author surface

| Конструкция | Судьба |
|-------------|--------|
| `@tab` как тип корня файла | **Composition**: import / catalog / host bundle |
| `tab … dashspec` в parent | Merge в composer; не навигация section/page |
| `standalone { }` | Composer: embed vs entry; фильтры **один раз** |
| `page` (ADR-0030, top-level) | **Migrate** → `section` (parse alias + deprecate) |
| `phase` | **Migrate** → `page` под тем же `section` |

### 4. Grammar (целевой скелет)

```text
report "Сводка"
  navigation
    initial section = peak_util
    initial page = main
  end navigation

  filters { … }

  section peak_util
    title = "№1 Закупка и утилизация"
    navigation
      show = bar
    end navigation
    page main
      toolbar period_grain, period_start, app_name, chart_top
      include layout "…"
      card kpi_over_limit
        widget main
          diagram demo_stakeholder_kpi_over_limit
          data … bind … end data
        end widget
      end card
    end page
  end section
end report
```

Целевой keyword **`widget`** может вводиться как обёртка над slot `diagram ref` + `data`; до P1 author мыслит widget, DSL может оставаться `view`/`diagram ref` с mapping в composer.

Переходный период: парсер принимает **legacy `page id`** (ADR-0030) как **`section id`** с diagnostic deprecate; вложенный `page` — лист внутри section.

### 5. Анализатор (author guardrails)

- `bind` на filter, не объявленный на report / section / page → **error**.
- Тот же filter на widget / card-local и на page/section → **error**.
- `navigation.show = hidden` при одном child — **допустимо**; Host не скрывает без узла.

## Relationship to prior ADRs

- [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md): цели сохраняются; top-level `page` → `section`, browse/detail → nested `page`.
- [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md): slot plane на **page** (toolbar + card grid); interior slots — **widget** + local filters внутри **card**.
- [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md): file modules ≠ `section` / `page`.

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | ADR-0073 + ADR-0074; author guide one-pager |
| **P1** | Composer: `section`/`page` + `navigation`; alias legacy `page`→`section` |
| **P2** | Migrate demo stakeholder; `phase` → `page` |
| **P3** | Portal soak via composed tree; drop synthetic tab nav |

## Consequences

- Речь автора: «раздел» = `section`, «лист» = `page`.
- Alias + codemod; Host только через composed view ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)).
