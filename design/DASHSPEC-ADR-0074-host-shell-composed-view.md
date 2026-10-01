# DASHSPEC-ADR-0074: Host as shell — composed view, no UI defaults

| | |
|---|---|
| **Status** | Accepted (composer contract: normative; Host refactor: incremental) |
| **Date** | 2026-10-01 |
| **Relates to** | [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0054](DASHSPEC-ADR-0054-dashhost-module.md), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md), [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md) |

## Context

Задумка продукта с [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md): **спека в git — SSOT**, Host — рендер DSL + data. [ADR-0054](DASHSPEC-ADR-0054-dashhost-module.md) вынес planet chrome (catalog, title, links) в `.dashhost`; **тело отчёта** всё равно должно быть полностью задано контентом, а не догадками Blazor.

Фактическое поведение Host и соседних сервисов частично **реактивное**:

| Поведение | Пример | Противоречие shell-модели |
|-----------|--------|---------------------------|
| Скрыть nav, если один child | `Tabs.Count > 1`, `Pages.Count > 1` | UI решает за автора |
| Выбрать активный экран | `ResetActivePageForTab()` → first page | Нет явного `initial` |
| Синтетическая вкладка | `@tab` module → один `TabDefinition` | Author tree ≠ runtime tree |
| Видимость toolbar-фильтров | пересечение с картами на странице | Toolbar не как в спеке |
| Embed vs entry | `standalone` игнорируется при merge | Два мира в одном файле без composer |

Это не «новая философия» — **возврат к исходному контракту**: Host **отобрази то, что передали**; политика и умолчания — **до** Host.

## Decision

### 1. Три слоя

```text
  Author DSL (.dashspec, layouts, diagrams)
           │
           ▼
  Composer (merge imports, embed, catalog entry, defaults policy)
           │
           ▼  ComposedDashboardView (immutable)
  Host Shell (Blazor: nav chrome, slots, widgets, viz — no inference)
```

- **Composer** — единственное место, где допустимы: merge модулей, подстановка `initial section/page`, разрешение `standalone`, построение полного списка nav-узлов и toolbar slots.
- **Host Shell** — только **projection** `ComposedDashboardView` + session state (значения фильтров, loading). **Запрещено** менять дерево навигации, скрывать уровни или дополнять фильтры по эвристикам.

Composer может жить в `DashSpec.Execution.*` / loader pipeline; Host зависит от **абстракции view**, не от сырого `DashboardDocument` с неявными правилами.

### 2. `ComposedDashboardView` (нормативный минимум)

Логическая структура (имена типов — implementation detail):

```text
ComposedDashboardView
  ReportTitle
  InitialRoute          # section id + page id — обязательны после compose
  NavigationBands[]     # ordered; each band is explicit
    BandKind              # report | section | page | portal (soak)
    ShowMode              # bar | hidden | … — from spec/composer only
    Items[]               # id, label, order — all children listed
  ActiveRoute             # from URL / session; composer sets InitialRoute only at load
  Chrome
    Toolbars[]            # per scope (report | section | page): slots + filters + apply
    FiltersChrome         # sticky, bar, apply mode — from spec
  LayoutPlanes[]          # per active page: slot grid ([ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md))
  Cards[]                 # instances on active page
```

Правила:

1. Если автор хочет **не показывать** полосу переключения — в composed tree для этого band **`ShowMode = hidden`** (или band отсутствует), а не `Count <= 1` в Razor.
2. Если **одна** page под section — nav band для page может быть `hidden` **явно** в spec или **явно** выставлен composer’ом по policy **с записью в артефакт** (debug/spec expand), но Host не содержит `if (count > 1)`.
3. **Portal** (несколько report в soak) — отдельный `NavigationBand` уровня `portal`, не смешивать с `section`/`page` одного report ([ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md)).

### 3. Initial route

Источники (в порядке приоритета для composer):

1. `report.navigation.initial` / `entry` block в `.dashcatalog` (`initial section`, `initial page`).
2. Явная policy в composer config (dev only) — **должна сериализоваться** в composed metadata для аудита.
3. **Запрещено** в Host: «взять первую page из списка» без записи в `InitialRoute`.

Catalog example:

```text
entry stakeholder
  dashspec "demo-stakeholder.dashspec"
  initial section = peak_util
  initial page = main
end entry
```

### 4. Toolbar и фильтры

- Список виджетов на toolbar = **слоты из composed chrome** для active screen/page.
- Host **не** вызывает `ToolbarFilterVisibility`-подобную эвристику для **сокращения** списка; если фильтр объявлен на toolbar scope — он рисуется (disabled state — отдельный явный флаг в view при необходимости).
- Привязка к SQL — по `bind` на card; анализатор ловит «bind без declare» на этапе compile ([ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md)).

### 5. Ответственность `.dashhost`

[ADR-0054](DASHSPEC-ADR-0054-dashhost-module.md): planet shell (catalog picker, product title, links). **Не** добавляет навигацию внутри report. Смена catalog entry → новый compose + новый `InitialRoute`.

### 6. Миграция с текущего Host

| Сейчас | Цель |
|--------|------|
| `DashboardPageController` + сырой `DashboardDocument` | Controller читает `ComposedDashboardView` |
| `DashboardTabBar` / `DashboardPagePicker` if count>1 | Один `NavigationBandView` component: renders band from view |
| `TabLayoutCompactor` + active page inference | Composer выдаёт layout plane для `(section, page)` |
| `MergeReferencedTabModules` в parse | Composer pipeline |
| Synthetic tab for `@tab` | Composer emits real `section`/`page` tree or portal band |

Инкрементально: composer может **сначала** эмулировать текущее поведение, но **все** бывшие умолчания должны быть **явными строками** в composed output (включая `show=hidden`), чтобы затем убрать дубли из Host.

## Non-goals

- Host не выполняет бизнес-merge SQL, не выбирает connector — без изменений.
- Composer **не** меняет значения фильтров пользователя — только структуру UI.

## Consequences

- Поведение отчёта **воспроизводимо** из composed artifact (тесты composer без Blazor).
- Авторы и агенты работают с деревом [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md); Host перестаёт быть «вторым автором».
- Краткосрочно возможен рост verbosity (`navigation show = hidden`); это намеренно — **явность вместо магии**.

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | ADR-0073/0074; fixture `ComposedDashboardView` JSON schema (dev) |
| **P1** | Composer wrapper: document → view; Host reads view for nav only |
| **P2** | Перенос filter toolbar list + initial route; удалить count>1 gates |
| **P3** | demo specs + catalog `initial`; deprecate Host heuristics |
