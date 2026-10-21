# DASHSPEC-ADR-0111: VisualModel и VisualCompositor — одна картина UI, Host = проекция

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-21 |
| **Relates to** | [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) (host как оболочка — сверка ниже), [ADR-0110](DASHSPEC-ADR-0110-host-shell-slot-plane.md) (плоскость оболочки), [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) / [ADR-0095](DASHSPEC-ADR-0095-dashboard-shell-wiring-pipeline.md) (конвейер компиляции), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md) (слоты), [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md), [ADR-0109](DASHSPEC-ADR-0109-filter-wiring-ssot.md) (SSOT фильтров), [ADR-0105](DASHSPEC-ADR-0105-filters-pipe-table.md), [ADR-0108](DASHSPEC-ADR-0108-eventsource-handler.md), [ADR-0101](DASHSPEC-ADR-0101-single-unit-file-type.md) / [ADR-0102](DASHSPEC-ADR-0102-one-language.md) (один язык) |

## Context

ADR-0074 (2026-10-01) определила три слоя: спека → композер → `ComposedDashboardView` → Host-проекция, но её характерная часть устарела за три недели смен языка ([ADR-0101]/[ADR-0102]) и фильтровой подсистемы ([ADR-0105]/[ADR-0109]). Проверка кода (2026-10-21) показала:

1. **Имя «композер» занято парсером**: F# `DashboardComposer.parse` (`src\DashSpec.Modeling.Parse\Document\DashboardComposer.fs`) — orchestration merge таб-модулей + `DocumentCompilePipeline.completeDocument` (фазы `MaterializeDashflow → BuildWiringGraph → ValidateDocument`). Слияние — легальная часть конвейера компиляции (ADR-0097), не хак, который «надо перенести в композер»: строка миграции ADR-0074 §6 «MergeReferencedTabModules → composer pipeline» отменяется.
2. **Грамматик из 0073/0074 не существует**: `navigation`, `section`, `initial section/page` — grep по F# равен нулю. Нынешняя навигационная модель: catalog entry (с `InitialTabId`) → `TabDefinition[]` → `ReportPageDefinition[]`.
3. **Эвристики в рантайме локализованы** (Surface, `DashboardPageController.cs`, 1510+ строк): «первая вкладка» (L1180), «первая страница» (L1420 `ResetActivePageForTab` + дубль в `TabLayoutCompactor.ResolveContext` L40–43), скрытие nav при `Count > 1` (`HasPages` L150, `HasCatalog` L140, `Home.razor:51,60`, `TopbarNav:23`, `DashboardCatalogPicker:5`, `DisplayResolution.ResolveTabLabel` L53–55), toolbar-видимость пересечением с картами (`ToolbarFilterVisibility.ResolveVisibleFilters` — ровно та эвристика, что §4 ADR-0074 запрещает Host'у), fallback-ряд тулбара (L1297–1321).

Оператор сформулировал суть: мы строим **VisualCompositor / VisualObjectModel** — одну картину UI из спеки, которую рисуют любые поверхности (Blazor-оболочка, nav-плагины, будущий headless-экспорт).

## Decision

### 1. Имена и место

| Сущность | Имя | Дом |
|----------|-----|-----|
| Артефакт-картина | **`VisualModel`** (термин `ComposedDashboardView` из ADR-0074 обновлён; структура — её нормативный минимум §2) | `DashSpec.Execution.Composition` *(новый проект; референсит Modeling.Parse + Runtime, как Execution.Core)* |
| Строитель картины | **`VisualCompositor`** — чистая функция/сервис: (CatalogDocument + DashboardDocument + политика) → VisualModel | там же |
| Merge/валидация спеки | остаётся в F# конвейере (ADR-0097) | Modeling.Parse |
| Сессия (активный маршрут, busy, значения фильтров, запомненный entry через `ICatalogUsageClient`) | остаётся в рантайме; Host рисует `VisualModel + session state`, для смены `(tab, page)` запрашивает plane у VisualCompositor (чистая функция, кэш на entry) | Runtime/Surface |

### 2. Контракт `VisualModel` (нормативный минимум — секции с живыми инстансами)

```csharp
public sealed record VisualModel(
    string EntryId, CatalogDocument Catalog,
    VisualRoute InitialRoute,                    // EntryId + TabId + PageId (+ записанная политика)
    IReadOnlyList<NavBand> NavBands,             // portal | section | page; Items[] id/label/order
    ShellModel Shell,                            // слоты host-доски: brand/catalog/nav/links/help/settings/spec_dev/content (ADR-0110)
    ChromeModel Chrome,                          // Toolbars[] по scope (spec-слоты!) + FiltersChrome (sticky/apply/…)
    PlacementGraph Placements,                   // собранный граф размещения: слоты × (Scope, placement) + recorded defaults (§2a)
    IReadOnlyList<VisualCard> Cards);            // членство карточек по (tab, page) — definition-level, без сессии

public enum NavBandKind { Portal, Section, Page }
public sealed record NavBand(NavBandKind Kind, string Id, string Label, NavBandItem[] Items);
```

- **Полосы маппируются на текущую модель** без «report»-уровня: `portal` = `CatalogDocument` (entry → items, группировка сохранена), `section` = `TabDefinition`, `page` = страницы таба. Переименование `tab→section` (ADR-0073) — отдельная миграция, контракт полос её не меняет.
- **Labels** разрешает композитор (вызывая `DisplayResolution`→`DisplayResolutionHost`-семантику в Execution) и пишет в `Items[].Label` — Host не резолвит заголовки.
- `ShowMode` присутствует в контракте, дефолт = явный `Bar` в артефакте; Host никогда не скрывает по Count. Грамматика `show = hidden` для полос — позже ([ADR-0073] §5), по реальному инстансу.
- Карточки: membership-уровень (`VisualCard { CardId, TabId, PageId }`). Динамическую видимость (`when`/phase/selection — «visibility state» по [ADR-0108]) исполняет детерминированная политика в Runtime; Host её не содержит.
- Взаимодействия не зашиваются в модель: карточки несут ссылки на spec-сущности; dispatch/эффекты — подсистема взаимодействий ([ADR-0108]).

### 2a. Граф размещения — явный артефакт (как граф документа)

Граф данных собран и экспортируется (`DocumentWiringGraphBuilder.build`, [ADR-0091]: узлы/рёбра, edgeKind flow|route|event) — у размещения аналогичного собранного артефакта нет: математика placement`а размазана (`TabLayoutCompactor` TabLayoutBoardResolver, fallback-ряды в контроллере, `DashboardLayoutHelper.ResolveInteriorPlacements`, `CardLocalFilterChromeCompactor`, `PlacementGridCss`). Решение: **`PlacementGraphBuilder`** — чистая сборка графы размещения (вход: `DashboardDocument`, `SpecLibrary?`):

- узлы — slot-инстансы `(Scope, placement)`: оболочка (ADR-0110), карточки на досках tab/page, тулбар-виджеты, view-слоты интерьеров;
- рёбра — вложенность (plane → slot, nest/group → inner) и **склейки с графом документа по id** (карточка ↔ data-slot; фильтр ↔ цель `placement flow`, [ADR-0096]);
- все умолчания записи в граф (family-defaults `PlacementDefaults.ForFamily`, fallback-ряды) — записаны, не «тихие».

`VisualModel` не держит собственных Plane-структур: композитор нарезает собранный граф по маршруту (shell | tab | page | interior). Это доводит до конца инвариант ADR-0072 «одна слотовая плоскость» — одна собранная структура вместо повторных выводов. `PlacementGridCss` остаётся проекцией на CSS и граф не дублирует.

### 3. Языковая добавка (минимальная, по духу ADR-0109 «умолчание — явно»)

Catalog entry получает `page <pageId>` симметрично `tab <tabId>`: `entry <id> … dashspec "<path>" [tab <tabId>] [page <pageId>]` → `CatalogEntryDefinition.InitialPageId: string option`. Композитор валидирует громко (неизвестная страница/вкладка — ошибка compose, ссылка на entry). `CatalogParser` fallback `default = entries[0]` (CatalogParser.fs L88) — заменяется записью умолчания, Host без `default` не остаётся.

### 4. Что забирает VisualCompositor (точные адреса)

| Эвристика сегодня | Становится |
|-------------------|------------|
| «Первая вкладка» (`Controller` L1180) | `InitialRoute.TabId` из entry `tab`/записанный «first» |
| «Первая страница» (`ResetActivePageForTab` L1420, дубль `TabLayoutCompactor` L40–43) | `InitialRoute.PageId` из entry `page`/записанный «first» для начального таба; при смене таба без `page` — записанная политика «first-of-tab» в артефакте |
| Скрытие nav при `Count > 1` (6 мест, §Context) | Банды без эвристик; дерево/бар рисует то, что объявлено |
| Label-эвристика `Tabs.Count > 1` в `DisplayResolution.ResolveTabLabel` | Label в `NavBands[].Items`, Host читает готовое |
| `ToolbarFilterVisibility.ResolveVisibleFilters` (пересечение с картами активной страницы) | **Не переносится как есть** (нарушала бы ADR-0074 §4): композитор выдаёт **спек-объявленные** тулбар-слоты; привязка `filtersToCards` идёт в артефакт как инфо-флаг `Bound` — рендер рисует disabled-состояние вместо скрытия |
| Fallback-ряд тулбара (L1297–1321) | Умолчание, записывается в `PlacementGraph` при сборке (та же математика строк) |
| План слотов страницы (`RecomputeTabPlacements` + `TabLayoutCompactor`) | `PlacementGraphBuilder` собирает граф размещения; Host берёт слайсы — проекция |

### 5. Тесты без Blazor

Проект `DashSpec.Platform.Session.Tests` (паттерн фейков уже готов: `StubCompiler`, `FakeResolver`, `FakePaths`, …); каталог композитор принимает **как вход**, не грузит сам (обход `HeadlessReportSession.LoadCatalogEntryAsync → NotSupportedException`). Существующие юнит-тесты `LayoutSlotEngineTests`/`ToolbarFilterVisibilityTests` остаются; новые — `VisualCompositorTests`: startup-политики (tab/page initial + запись «first»), bands из каталога/табов, initial page grammar, artifact JSON (контракт артефакта = fixture по ADR-0074 P0).

## Rejected

| Идея | Почему |
|------|--------|
| Перенос merge таб-модулей в рантайм-композитор | Merge — конвейер компиляции (ADR-0097); дублировать — второй движок |
| Универсальное дерево «визуальных узлов» (DOM-стиль) | Сущность без второго инстанса; секции контракта растут от реальных поверхностей (Blazor, экспорт) |
| Оставить имя `ComposedDashboardView` | Термин «dashboard» не покрывает оболочку (ADR-0110); переименование дёшево — мы единственные авторы |
| Скрывать тулбар-фильтры эвристикой пересечения (внутри композитора) | Нарушает ADR-0074 §4; честный путь — slots + `Bound`-флаг + disabled-состояние |

## Migration (ломающая, авторы — только мы)

| Сегодня | Цель |
|---------|------|
| `DashboardPageController`: first-tab/first-page/count-гейты/labels (L1180, L1420, L150, L140, L794, L1297, L1451) | Читает `VisualModel` + session; ретрофит поэтапный, гейты удаляются |
| `Home.razor` TabBar/PagePicker с `Count > 1` | Полосы — из `NavBands` (рендер: временно существующие компоненты, далее `nav_tree`-плагин ADR-0110) |
| `ToolbarFilterVisibility` в цепочке | Спек-слоты + `Bound`-флаг; тесты флага — вместо видимости |
| Catalog без `default` → первую запись | Ошибка compose/парса с распечаткой умолчаний |

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | Проект `DashSpec.Execution.Composition`: **`PlacementGraphBuilder`** (сборка графа размещения с записью умолчаний), типы `VisualModel`, `VisualCompositor` (bands + initial route + labels, слайсы графа), JSON-артефакт, тесты без Blazor |
| **P1** | Catalog grammar `page <id>` + валидация; запись умолчаний (default entry, first tab/page) в артефакт |
| **P2** | `Shell`-секция из host-доски (ADR-0110 P1); ретрофит Host: bands из артефакта, удаление гейтов/эвристик §4; `nav_tree`-плагин |
| **P3** | Chrome: спек-тулбары + `Bound`-флаг вместо пересечения; placements-политика в композиторе |

## Consequences

- Одна картина UI = один источник для Blazor, плагинов и headless-экспорта; поведение воспроизводимо из артефакта.
- `DashboardPageController` худеет до проекции + сессии; Surface перестаёт быть «вторым автором».
- Сверка с ADR-0074 в части merge и toolbar-эвристики зафиксирована здесь; ADR-0110 обновлена на термин `VisualModel`.
