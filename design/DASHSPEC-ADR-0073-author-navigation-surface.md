# DASHSPEC-ADR-0073: Author navigation surface — `report` → `screen` → `page` → `card` → `diagram`

| | |
|---|---|
| **Status** | Accepted (grammar + composer: planned; Host: align incrementally) |
| **Date** | 2026-10-01 |
| **Relates to** | [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md), [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) |

## Context

Продукт изначально задумывался как **декларативный отчёт в git** → Host рисует то, что описано ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)). На практике авторы и агенты сталкиваются с другой моделью:

- **`page`** в DSL — экран отчёта (№1, Сводка), но слово не совпадает с BI-«вкладкой»;
- **`tab`** — одновременно вкладка soak, тип файла `@tab`, merge-модуль ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) и синтетическая вкладка в рантайме;
- **`phase browse | detail`** — отдельная ось, хотя по смыслу это **листы внутри одного раздела**;
- переключатели в UI используют один визуальный паттерн для разных уровней.

Исполнитель, который **уже знает**, что хочет («отчёт → раздел → лист → виджет»), вынужден держать карту платформы. Это не задача автора — признак размытой **навигационной поверхности**.

[ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md) ввёл `page` как аналитический экран; цель совпадает с этим ADR, но **именование и вложенность** не совпадают с естественной иерархией TabbedControl.

## Decision

### 1. Каноническая иерархия (author + agent)

Единственная цепочка **навигации и компоновки контента** внутри catalog entry:

```text
report
  screen
    page
      card
        diagram
```

| Уровень | Смысл | Навигация в UI |
|---------|--------|----------------|
| **report** | Один сценарий в catalog entry | Заголовок / смена entry |
| **screen** | Крупный раздел отчёта (№1, №2, Сводка…) | Явный узел в composed tree |
| **page** | Лист внутри раздела (browse / detail, варианты одного сценария) | Явный узел в composed tree |
| **card** | Виджет на layout-сетке | Нет |
| **diagram** | Kind + привязки колонок (часто `!include`) | Нет |

**Фильтры сценария** объявляются на **page** (минимум) или на **screen**, если общие для всех page внутри. **card** не объявляет глобальные фильтры; только `bind` к уже объявленным именам. Локальные исключения — явный author block (см. [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) — без скрытых правил в Host).

### 2. Соответствие demo stakeholder (пример)

Четыре сценария заказчика — **четыре `screen`** в одном `report`, каждый с одной или несколькими `page`:

```text
report stakeholder
  screen peak_util
    page main
      card …
  screen multi_app
    page browse
      card …
    page detail
      card …
  screen idle
    page main
      card …
  screen executive_summary
    page main
      card …
```

Текущий `phase browse | detail` → **`page browse` / `page detail`** под одним `screen`. Текущий `page peak_util` → **`screen peak_util`** + `page main` (или несколько page, если сценарий требует).

Точное разбиение №1–Сводка на screen vs page — **решает автор в спеке**; Host не схлопывает уровни (см. ADR-0074).

### 3. Что уходит с author surface

| Конструкция | Судьба |
|-------------|--------|
| `@tab` как тип корня файла | **Composition**: `import` / catalog / host bundle — не уровень навигации в author tree |
| `tab … dashspec` в parent | Остаётся **механизм сборки**; результат merge — composed tree, не «вкладка» в смысле screen/page |
| `standalone { }` | **Composer**: режим embed vs entry; автор пишет фильтры **один раз** |
| `page` (ADR-0030) | **Migrate** → `screen` (или alias на переходный период) |
| `phase` | **Migrate** → `page` под тем же `screen` |

### 4. Grammar (целевой скелет)

```text
report "Сводка"
  navigation
    initial screen = peak_util
    initial page = main
  end navigation

  filters { … }                    # report-wide, если нужно

  screen peak_util
    title = "№1 Закупка и утилизация"
    navigation
      show = bar                   # явно: bar | hidden | … — без Host-умолчаний
    end navigation
    page main
      toolbar period_grain, period_start, app_name, chart_top
      include layout "…"
      card …
    end page
  end screen
end report
```

Детали ключей `navigation` — в [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) (composed output). Парсер может принимать **`page` как alias `screen`** на переходный период с diagnostic deprecate.

### 5. Анализатор (author guardrails)

- `bind` на filter, не объявленный на report / screen / page активного контекста → **error** (одно предложенное исправление).
- Дублирование того же filter на card и на page/screen → **error** (глобальный фильтр только выше card).
- Пустой `navigation.show = hidden` при одном child — **допустимо**; Host всё равно не «угадывает» скрытие без узла.

## Relationship to prior ADRs

- [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md): цели (экран сценария, browse/detail, catalog `group`) **сохраняются**; меняются имена и вложенность.
- [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md): slot plane применяется к **page** (toolbar + card grid), не к смешанным «tab/page» в голове автора.
- [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md): file modules остаются для reuse; **не** путать с `screen`/`page`.

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | ADR-0073 + ADR-0074; author guide one-pager |
| **P1** | Composer: `screen`/`page` model + `navigation` block; parse alias `page`→`screen` |
| **P2** | Migrate demo stakeholder; `phase` → `page`; deprecate diagnostics |
| **P3** | Remove synthetic `TabDefinition` navigation from author path; soak via composed portal tree |

## Consequences

- Агент и человек мыслят **одним деревом** без перевода «page значит вкладка, tab значит файл».
- Breaking rename смягчается alias + codemod; Host меняется только через composed view ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)).
