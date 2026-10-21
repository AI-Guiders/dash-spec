# DASHSPEC-ADR-0111: VisualGraph — SSOT визуальной композиции (Компоновщик)

| | |
|---|---|
| **Status** | Accepted (пере-рамка того же дня: ранние правки `be18e71`/`4e490a4` — «VisualModel / VisualCompositor» — отменены §Rejected; C#-контракт ранней правки этого же дня тоже отменён — модель дома пишется на F#) |
| **Date** | 2026-10-21 |
| **Relates to** | [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) (host как оболочка — сверка ниже), [ADR-0110](DASHSPEC-ADR-0110-host-shell-slot-plane.md) (плоскость оболочки), [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) / [ADR-0095](DASHSPEC-ADR-0095-dashboard-shell-wiring-pipeline.md) (конвейер компиляции), [ADR-0091](DASHSPEC-ADR-0091-document-wiring-graph.md) (граф данных), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md) (слоты), [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md), [ADR-0109](DASHSPEC-ADR-0109-filter-wiring-ssot.md) (SSOT фильтров), [ADR-0105](DASHSPEC-ADR-0105-filters-pipe-table.md), [ADR-0108](DASHSPEC-ADR-0108-eventsource-handler.md), [ADR-0101](DASHSPEC-ADR-0101-single-unit-file-type.md) / [ADR-0102](DASHSPEC-ADR-0102-one-language.md) (один язык) |

## Context

ADR-0074 (2026-10-01) определила: спека → композер → `ComposedDashboardView` → Host-проекция. Проверка кода (2026-10-21) показала (адреса — по факту):

1. **Имя «композер» занято парсером**: F# `DashboardComposer.parse` — merge таб-модулей + `DocumentCompilePipeline.completeDocument` (фаза `BuildWiringGraph` внутри). Слияние — легальная часть конвейера компиляции ([ADR-0097]); строка миграции ADR-0074 §6 «MergeReferencedTabModules → composer pipeline» отменена.
2. **Грамматик `navigation/section/initial` из 0073/0074 не существует** (grep по F# — ноль). Навигационная модель: catalog entry (`InitialTabId`) → `TabDefinition[]` → `ReportPageDefinition[]`.
3. **Геометрия считается в момент рендера, в хосте, на C#**: fallback-ряд тулбара и «первая вкладка/страница» в `DashboardPageController` (L1180, L1297–1321, `ResetActivePageForTab` L1420, дубль в `TabLayoutCompactor` L40–43), интерьеры — `DashboardLayoutHelper.ResolveInteriorPlacements` на рендере, скрытие nav по `Count > 1` (6 мест), видимость тулбар-фильтров пересечением с картами (`ToolbarFilterVisibility` — ровно то, что §4 ADR-0074 запрещает Host'у). Корень размаха: математика размещения родилась в слое рендера (**C#, `Execution.Runtime`**: `LayoutSlotEngine`/`TabLayoutPlanner`/`TabLayoutCompactor`/`ToolbarLayoutCompactor`/`CardInterior*`/`LayoutBoardRowPlacer`/`FilterPlacementAnalyzer` — ядро ~1,2–1,5 тыс. строк чистой функции), вне модельного слоя, и потому никогда не попадала в конвейер.

Оператор (2026-10-21, постановка рамки): отделить структуру от рендера — рендер только размещает по уже собранной модели; сборку структуры свести в SSOT. Паттерн — **Компоновщик (Composite)**:

```
Grid  (Rows, Cols, Cells)
Cell  (Content)
Content = Grid | Item
```

Контейнер сегодня — грид, но по сути им может быть что угодно: форма узла его не прибивает. Существующая грамматика уже является сериализацией этого дерева: `layout [ products:3 users:9 ]` в карточке — интерьерный грид; доска таба — грид страницы; тулбары и зоны оболочки ([ADR-0110]) — гриды других scope; nest/group ([ADR-0063]/[ADR-0056]) — вложенные гриды. SSOT не изобретает язык — доводит до конца то, что язык уже декларирует.

## Decision

### 1. Артефакт — `VisualGraph`, дерево компоновки; модель — F#

Прецедент — граф данных: типы `WiringNode`/`WiringEdge` — F#-записи в **`DashSpec.Modeling.Core`** (`WiringGraph.fs`); сборка — F#-фаза конвейера. VisualGraph идёт тем же путём, и F# ложится на Компоновщик дословно — union `VisualContent = VGrid | VItem` — это буквально `Content = Grid | Item`:

```fsharp
// DashSpec.Modeling.Core / VisualGraph.fs (рядом с WiringGraph.fs)

type VisualScope =
    | Shell
    | NavBand
    | TabBoard
    | PageToolbar
    | CardInterior

type PlacementProvenance =
    | Declared            // автор объявил ячейку в спеке
    | RecordedDefault     // сборка подставила умолчание и ЗАПИСАЛА его (не «тихое»)

type CellPlacement =
    { Row: int; Col: int; RowSpan: int; ColSpan: int }   // посчитано после компакции —
                                                        // финальная геометрия, не декларация

type VisualItemKind =
    | Card
    | Filter
    | NavEntry
    | Widget

type VisualContent =
    | VGrid of VisualGrid
    | VItem of VisualItem

and VisualGrid =
    { Rows: int
      Cols: int
      Cells: VisualNode list }

and VisualNode =
    { SlotName: string              // имя слота со смыслом: products, nav, app_period —
                                    // те же имена, по которым склеиваются клики/фильтры
      Scope: VisualScope
      Placement: CellPlacement
      Provenance: PlacementProvenance
      Content: VisualContent }

and VisualItem =
    { ItemRef: string               // ид карточки/фильтра/полосы/виджета: склейка
                                    // с WiringGraph ([ADR-0091]) по id
      Kind: VisualItemKind }
```

- **Item — ссылка, не копия.** `ItemRef` склеивается с графом данных по id. Два SSOT остаются двумя: данные — у данных, структура — у структуры. Членство карточки в (tab, page) — позиция узла, отдельных списков членства не существует.
- **Grid — единственный контейнер сегодня**, но union `VGrid | VItem` форму не прибивает: будущий stack/dock — ещё одна альтернатива, спека и сборка не меняются. Второй контейнер — по реальному инстансу ([ADR-0108]).
- **NavBands** — тоже компоновщик: полоса (`portal|section|page`) — грид со слотами-`VItem` на записи каталога/табы/страницы. `InitialRoute` — записанное умолчание артефакта (`RecordedDefault`).

### 2. Сборка — F#-фаза конвейера, один раз на ревизию документа

`VisualGraphBuilder` — F#-модуль в **`DashSpec.Modeling.Parse`** (рядом с `DocumentWiringGraphBuilder`), чистая функция над слитым валидированным документом (+ каталог): фаза `completeDocument … → BuildWiringGraph → BuildVisualGraph`. Артефакт едет с результатом компиляции рядом с wiring-графом — тот же жизненный цикл: одна сборка на ревизию документа, никакой «нарезки по маршруту» и никакого пересчёта на рендере.

**Математика размещения мигрирует из C# в F# по scope** — это и есть уборка корня размаха: placement родился в слое рендера, вне модели; домой он переезжает в фазу сборки. Порядок — по фазам ниже, каждая миграция с сохранением видимого поведения и тестов. В C# остаются только проекции: `PlacementGridCss` (CSS-проекция узла) и обход в Blazor — это презентация, не модель.

**Громкая семантика** ([ADR-0109]): два предмета в одну ячейку — ошибка сборки; неизвестный `ItemRef` — ошибка сборки; умолчания (family-defaults, fallback-ряды, first-tab/first-page) — записываются с `RecordedDefault`, а не применяются тихо.

### 3. Сессия — оверлей, не мутация

Fold (`fold = independent` уже в спеках), занятость, активная `(tab, page)`, выбор — состояние сессии в хосте, **наложенное** поверх иммутабельного графа при рендере. Граф не мутирует от взаимодействия. Эта граница проведена жёстко: ранняя редакция с «композитором, нарезающим по маршруту», отменена — нарезка по активному маршруту и есть сессия (§4 ADR-0074: «активный выбор, busy — это сессия, а не композер»).

### 4. Рендер — тупой обход

`VGrid` → контейнер раскладки (CSS-грид — проекция узла; `PlacementGridCss` из вычислителя становится проектором), `VItem` → отрисовать то, на что ссылка, вложенный `VGrid` → рекурсия. В контроллере умирают `RecomputeToolbarPlacements`, per-render `ResolveInteriorPlacements`, гейты `Count > 1`, label-эвристики — всё становится чтением графа + сессионного оверлея:

| Эвристика сегодня | Становится |
|-------------------|------------|
| «Первая вкладка» (`DashboardPageController` L1180) | `InitialRoute.TabId` — записанное умолчание |
| «Первая страница» (`ResetActivePageForTab` L1420, дубль `TabLayoutCompactor` L40–43) | `InitialRoute.PageId` — записанное умолчание |
| Скрытие nav при `Count > 1` (6 мест) | Рисуется объявленное; умолчание — записано |
| Label-эвристика `Tabs.Count > 1` (`DisplayResolution.ResolveTabLabel`) | Label в узле, Host читает готовое |
| `ToolbarFilterVisibility` (пересечение с картами) | Спек-объявленные слоты + флаг `Bound`; рендер рисует disabled, не прячет (§4 ADR-0074) |
| Fallback-ряд тулбара (L1297–1321) | Умолчание, записанное в граф при сборке |
| Планы слотов (`TabLayoutPlanner`/`LayoutSlotEngine` на рендере) | Собраны в граф (F#-фаза); Host — проекция |

### 5. Языковая добавка (минимальная, по духу ADR-0109)

Catalog entry получает `page <pageId>` симметрично `tab <tabId>`: `InitialPageId: string option`. Сборка валидирует громко (неизвестная страница/вкладка — ошибка, ссылка на entry). Fallback каталога «первая запись» — заменяется записанным умолчанием.

### 6. Приёмка фундамента — визуальная

Фундамент принимается не сам по себе, а домом: conformance-цели — экраны образца («взрослый BI», HTML-образец Сергея). Дерево считается готовым по scope, когда тупая проекция этого scope даёт задуманный вид. Фундамент не уезжает в абстракцию, потому что его проверяют глаза.

## Rejected

| Идея | Почему |
|------|--------|
| `VisualModel` + `VisualCompositor` как сущности (ранняя редакция `be18e71`/`4e490a4`) | Композитор выжил бы с чужой работой: «нарезка по маршруту» — сессия, а не слой; артефакт несёт все scope сразу, хост делает lookup. Картина = сам граф, склеенный с wiring-графом по id |
| **C#-контракт типов и C#-билдер в `Execution.Runtime`** (правка этого же дня) | Модель дома пишется на F# (`Modeling.Core`/`Modeling.Parse` — прецедент `WiringGraph`); сборка на C# сбоку конвейера увековечила бы F#/C#-раздвоение размещения — ту самую причину размаха, которую убираем |
| Новый проект для P0 | Типы — в `Modeling.Core` (рядом с `WiringGraph.fs`), сборка — в `Modeling.Parse` (рядом с `DocumentWiringGraphBuilder`); отдельный проект — по реальной нужде |
| Перенос merge таб-модулей в рантайм | Merge — конвейер компиляции ([ADR-0097]); дублировать — второй движок |
| Универсальное DOM-подобное дерево без секций | Сущность без второго инстанса ([ADR-0108]); scope растут от живых поверхностей |
| Скрывать тулбар-фильтры эвристикой пересечения | Нарушает §4 ADR-0074; честный путь — слоты + `Bound` + disabled-состояние |

## Migration (ломающая, авторы — только мы)

| Сегодня | Цель |
|---------|------|
| Математика размещения — C#, `Execution.Runtime`, зовётся на рендере | F#-фазы сборки `BuildVisualGraph`; ядро ~1,2–1,5 тыс. строк мигрирует по scope |
| `DashboardPageController`: first-tab/first-page/count-гейты/labels/fallback-ряд | Читает граф + сессию; гейты удаляются по одному, поведение сохраняется на каждом шаге |
| `Home.razor` TabBar/PagePicker с `Count > 1` | Полосы — узлы графа (рендер: существующие компоненты, далее `nav_tree`-плагин [ADR-0110]) |
| `ToolbarFilterVisibility` в цепочке | Спек-слоты + `Bound`; тесты флага вместо видимости |
| `PlacementGridCss` вычисляет | Проекция узла (остаётся C# — презентация) |
| Каталог: «первая запись» по умолчанию | Ошибка с записанным умолчанием |

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | Типы `VisualGraph` (F#, `Modeling.Core`) + фаза `BuildVisualGraph` + **миграция интерьеров карточек** (самая зрелая математика — `CardInterior*`/`LayoutSlotEngine.PlanCardInterior` → F#): сборка в конвейере, дамп графа (conformance-fixture), headless-тесты без Blazor |
| **P1** | Scope `TabBoard` + `PageToolbar`: миграция `TabLayout*`/`ToolbarLayoutCompactor`/fallback-рядов; хост читает граф; по одному убийству `RecomputeToolbarPlacements`/per-render резолвов, видимое поведение сохранено |
| **P2** | Scope `Shell` (зоны [ADR-0110]) + `NavBand` полосы + записанный `InitialRoute`; языковая добавка `page <id>` в каталоге; миграция `FilterPlacementAnalyzer` |
| **P3** | Экспорт для Studio (два графа, склеенные по id, одним полотном) + визуальные conformance-экраны |

## Consequences

- Одна структура UI = один источник для Blazor, плагинов, Studio и headless-экспорта; воспроизводимость из артефакта.
- Модель — на F# (`Modeling.Core`), сборка — фаза конвейера (`Modeling.Parse`): F#/C#-раздвоение размещения — та самая причина размаха — устраняется; в C# остаются только проекции.
- `DashboardPageController` худеет до обхода + сессии; Surface перестаёт быть «вторым автором».
- Визуальная итерация — правка декларации + дамп дерева: собрал, посмотрел композицию целиком.
- Два SSOT — данные (wiring-граф, [ADR-0091]) и структура (VisualGraph) — склеены по id; никакого дублирования истины.
- [ADR-0110] обновлена на термин `VisualGraph`.
