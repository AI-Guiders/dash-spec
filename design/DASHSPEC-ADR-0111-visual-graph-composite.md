# DASHSPEC-ADR-0111: VisualGraph — SSOT визуальной композиции (Компоновщик)

| | |
|---|---|
| **Status** | Accepted (пере-рамка того же дня: `be18e71`/`4e490a4` — «VisualModel / VisualCompositor» — отменены §Rejected; ранние правки этого же дня — C#-контракт, затем F#-фаза с портом математики — тоже отменены §Rejected) |
| **Date** | 2026-10-21 |
| **Relates to** | [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) (host как оболочка — сверка ниже), [ADR-0110](DASHSPEC-ADR-0110-host-shell-slot-plane.md) (плоскость оболочки), [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) / [ADR-0095](DASHSPEC-ADR-0095-dashboard-shell-wiring-pipeline.md) (конвейер компиляции), [ADR-0091](DASHSPEC-ADR-0091-document-wiring-graph.md) (граф данных), [ADR-0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md) (слоты), [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md), [ADR-0109](DASHSPEC-ADR-0109-filter-wiring-ssot.md) (SSOT фильтров), [ADR-0105](DASHSPEC-ADR-0105-filters-pipe-table.md), [ADR-0108](DASHSPEC-ADR-0108-eventsource-handler.md), [ADR-0101](DASHSPEC-ADR-0101-single-unit-file-type.md) / [ADR-0102](DASHSPEC-ADR-0102-one-language.md) (один язык) |

## Context

ADR-0074 (2026-10-01) определила: спека → композер → `ComposedDashboardView` → Host-проекция. Проверка кода (2026-10-21) показала (адреса — по факту):

1. **Имя «композер» занято парсером**: F# `DashboardComposer.parse` — merge таб-модулей + `DocumentCompilePipeline.completeDocument` (фаза `BuildWiringGraph` внутри). Слияние — легальная часть конвейера компиляции ([ADR-0097]); строка миграции ADR-0074 §6 «MergeReferencedTabModules → composer pipeline» отменена.
2. **Грамматик `navigation/section/initial` из 0073/0074 не существует** (grep по F# — ноль). Навигационная модель: catalog entry (`InitialTabId`) → `TabDefinition[]` → `ReportPageDefinition[]`.
3. **Геометрия считается в момент рендера, в хосте**: fallback-ряд тулбара и «первая вкладка/страница» в `DashboardPageController` (L1180, L1297–1321, `ResetActivePageForTab` L1420, дубль в `TabLayoutCompactor` L40–43), интерьеры — `DashboardLayoutHelper.ResolveInteriorPlacements` на рендере, скрытие nav по `Count > 1` (6 мест), видимость тулбар-фильтров пересечением с картами (`ToolbarFilterVisibility` — ровно то, что §4 ADR-0074 запрещает Host'у).

При этом **вся математика и весь словарь уже существуют и используются** (`Execution.Runtime`: `TabLayoutBoardResolver`/`TabLayoutCompactor`/`TabLayoutPlanner`/`ToolbarLayoutCompactor`/`LayoutSlotEngine`/`CardInterior*`/`LayoutBoardRowPlacer`/`FilterPlacementAnalyzer`; словарь идентичности — `WiringNode.Id`/`WiringNodeKind`, `CardFlowInput.NodeId`, `PlacementDefinition` в `LayoutTypes.fs`). Не существует одного: **собранного графа композиции** — вывод этой математики нигде не сходится в один артефакт, а разлетается по вызовам на рендере.

Оператор (2026-10-21, постановка рамки): отделить структуру от рендера — рендер только размещает по уже собранной модели; сборку структуры свести в SSOT. Паттерн — **Компоновщик (Composite)**:

```
Grid  (Rows, Cols, Cells)
Cell  (Content)
Content = Grid | Item
```

Контейнер сегодня — грид, но по сути им может быть что угодно: форма узла его не прибивает. Существующая грамматика уже является сериализацией этого дерева: `layout [ products:3 users:9 ]` в карточке — интерьерный грид; доска таба — грид страницы; тулбары и зоны оболочки ([ADR-0110]) — гриды других scope; nest/group ([ADR-0063]/[ADR-0056]) — вложенные гриды. SSOT не изобретает язык и не изобретает словарь — существующее продолжает использоваться, просто теперь всё это **потребляет один граф**: вывод математики ложится в него при сборке, рендер читает его.

## Decision

### 1. Артефакт — `VisualGraph`; словарь существующий, новый — только рекурсия компоновки

Прецедент — `WiringGraph` (`DashSpec.Modeling.Core`, `WiringGraph.fs`): узел несёт `Id + Kind + Scope`, рёбра ссылаются строками, никаких ref-типов. `VisualGraph` живёт рядом и берёт словарь как есть — новый тип ровно один: рекурсия `Content = Grid | Item`:

```fsharp
// DashSpec.Modeling.Core / VisualGraph.fs — рядом с WiringGraph.fs

type PlacementProvenance =
    | Declared            // автор объявил ячейку в спеке
    | RecordedDefault     // сборка подставила умолчание и ЗАПИСАЛА его (не «тихое», [ADR-0109])

type VisualContent =
    | VGrid of VisualGrid     // вложенный контейнер (nest / group / зона / интерьер)
    | VLeaf                   // лист: сущность — сам узел (Id + Kind), ref-типов нет

and VisualGrid =
    { Rows: int
      Cols: int
      Cells: VisualNode list }

and VisualNode =
    { Id: string                        // тот же Id, что у WiringNode: ид карточки, имя фильтра,
                                        // имя слота ('products'), id полосы — склейка двух графов
      Kind: WiringNodeKind              // существующий словарь (Card | Slot | Filter | Chrome | Page | …)
      Scope: string option              // существующая конвенция scope-строк WiringNode:
                                        // "shell" | "tab:{id}" | "page:{id}" | "card:{id}"
      Placement: PlacementDefinition    // существующая запись { Row; Col; Span } (LayoutTypes.fs)
                                        // — посчитана после компакции, финальная геометрия
      Provenance: PlacementProvenance
      Content: VisualContent }
```

- **Никаких новых ref-типов и второго словаря**: узел листа — это сам узел (`Id + Kind`); склейка с графом данных — тот же `Id` (и тот же `Kind`) в обоих графах, как рёбра `WiringEdge` ссылаются строками. Если у визуальной сущности нет двойника в wiring-словаре — новый кейс `WiringNodeKind` по живому инстансу ([ADR-0108]), не параллельный енум.
- **`PlacementDefinition` — один тип на размещение**: если вычисленной геометрии понадобится больше полей (row span и т.п.) — расширяется он, а не заводится второй.
- **Item — ссылка, не копия**: в листе нет содержимого карточки, только `Id + Kind`; данные — у wiring-графа. Членство карточки в (tab, page) — позиция узла, отдельных списков членства нет.
- **Grid — единственный контейнер сегодня**, но union `VGrid | VItem` форму не прибивает: будущий stack/dock — ещё одна альтернатива, спека и сборка не меняются. Второй контейнер — по реальному инстансу.
- **NavBands** — тоже компоновщик: полоса (`portal|section|page`) — грид со слотами-листами на записи каталога/табы/страницы (Id по ним). `InitialRoute` — записанное умолчание артефакта (`RecordedDefault`).

### 2. Сборка — одна точка, один раз на ревизию; вся существующая математика потребляется как есть

`VisualGraphBuilder` — в `DashSpec.Execution.Runtime`, **рядом с математикой, которую он вызывает**: (`DashboardDocument` + `CatalogDocument`) → `VisualGraph` (типы — F# из `Modeling.Core`). Существующие резолверы/компакторы/планировщики (`TabLayoutBoardResolver`, `TabLayoutCompactor`, `TabLayoutPlanner`, `ToolbarLayoutCompactor`, `LayoutSlotEngine`, `CardInterior*`, `LayoutBoardRowPlacer`, `FilterPlacementAnalyzer`) **остаются на своих местах и продолжают использоваться — их вывод ложится в узлы графа**. Никакого порта: переписывание работающей математики без второго инстанса — churn ([ADR-0108]).

Точка сборки — **bootstrap загрузки отчёта** (`ReportSpecBootstrap`): один вызов на загрузку/ревизию документа, артефакт едет в bootstrap-результате рядом с документом и wiring-графом. Параллель имён: `DocumentWiringGraphBuilder` (данные) + `VisualGraphBuilder` (структура) — модель у обоих F# (`Modeling.Core`), билдер каждого живёт там, где его вывод считается.

Артефакт **маршруто-независим**: все scope сразу — зоны оболочки, доски всех табов, тулбары всех страниц, интерьеры всех карточек, все полосы. Никакой «нарезки по маршруту» при сборке: смена вкладки в хосте — lookup слайса, не пересборка.

**Громкая семантика** ([ADR-0109]): два предмета в одну ячейку — ошибка сборки; неизвестный `Id` (нет узла в wiring-графе) — ошибка сборки; умолчания (family-defaults, fallback-ряды, first-tab/first-page) — записываются с `RecordedDefault`, а не применяются тихо.

### 3. Сессия — оверлей, не мутация

Fold (`fold = independent` уже в спеках), занятость, активная `(tab, page)`, выбор — состояние сессии в хосте, **наложенное** поверх иммутабельного графа при рендере. Граф не мутирует от взаимодействия. Эта граница проведена жёстко: ранняя редакция с «композитором, нарезающим по маршруту», отменена — нарезка по активному маршруту и есть сессия (§4 ADR-0074: «активный выбор, busy — это сессия, а не композер»).

### 4. Рендер — тупой обход: существующий код потребляет граф

Рендерный путь меняет вход, не устройство: `VGrid` → контейнер раскладки (`PlacementGridCss` из вычислителя становится проектором узла), `VLeaf` → отрисовать сущность по `Id + Kind` из wiring-графа/документа, вложенный `VGrid` → рекурсия. В контроллере умирают `RecomputeToolbarPlacements`, per-render `ResolveInteriorPlacements`, гейты `Count > 1`, label-эвристики — всё становится чтением графа + сессионного оверлея:

| Эвристика сегодня | Становится |
|-------------------|------------|
| «Первая вкладка» (`DashboardPageController` L1180) | `InitialRoute.TabId` — записанное умолчание |
| «Первая страница» (`ResetActivePageForTab` L1420, дубль `TabLayoutCompactor` L40–43) | `InitialRoute.PageId` — записанное умолчание |
| Скрытие nav при `Count > 1` (6 мест) | Рисуется объявленное; умолчание — записано |
| Label-эвристика `Tabs.Count > 1` (`DisplayResolution.ResolveTabLabel`) | Label в узле, Host читает готовое |
| `ToolbarFilterVisibility` (пересечение с картами) | Спек-объявленные слоты + флаг `Bound`; рендер рисует disabled, не прячет (§4 ADR-0074) |
| Fallback-ряд тулбара (L1297–1321) | Умолчание, записанное в граф при сборке |
| `TabLayoutPlanner`/`LayoutSlotEngine` на рендере | Потребляются сборкой; их вывод — в графе, Host читает |

### 5. Языковая добавка (минимальная, по духу ADR-0109)

Catalog entry получает `page <pageId>` симметрично `tab <tabId>`: `InitialPageId: string option`. Сборка валидирует громко (неизвестная страница/вкладка — ошибка, ссылка на entry). Fallback каталога «первая запись» — заменяется записанным умолчанием.

### 6. Приёмка фундамента — визуальная

Фундамент принимается не сам по себе, а домом: conformance-цели — экраны образца («взрослый BI», HTML-образец Сергея). Дерево считается готовым по scope, когда тупая проекция этого scope даёт задуманный вид. Фундамент не уезжает в абстракцию, потому что его проверяют глаза.

## Rejected

| Идея | Почему |
|------|--------|
| `VisualModel` + `VisualCompositor` как сущности (`be18e71`/`4e490a4`) | Композитор выжил бы с чужой работой: «нарезка по маршруту» — сессия, а не слой; артефакт несёт все scope сразу, хост делает lookup. Картина = сам граф, склеенный с wiring-графом по Id |
| C#-контракт типов (правка этого же дня) | Модель дома — на F# (`Modeling.Core`, прецедент `WiringGraph.fs`) |
| F#-фаза конвейера с портом математики (правка этого же дня) | Вся существующая математика продолжает использоваться — билдер потребляет её вывод; порт работающей математики без второго инстанса — churn ([ADR-0108]). Билдер каждого графа живёт там, где считается его вывод: wiring — F#-фаза (его вывод всегда был F#-стороной), visual — C# у математики, типы у обоих F# |
| `ItemRef`/`VisualItemKind` — отдельные ref-типы (правка этого же дня) | Узел листа — сам узел: `Id + Kind` по конвенции `WiringNode`; склейка — тот же Id в обоих графах; второй словарь = параллельная истина |
| Новый проект для P0 | Типы — `Modeling.Core`, билдер — `Execution.Runtime`; отдельный проект — по реальной нужде |
| Перенос merge таб-модулей в рантайм | Merge — конвейер компиляции ([ADR-0097]); дублировать — второй движок |
| Универсальное DOM-подобное дерево без секций | Сущность без второго инстанса ([ADR-0108]); scope растут от живых поверхностей |
| Скрывать тулбар-фильтры эвристикой пересечения | Нарушает §4 ADR-0074; честный путь — слоты + `Bound` + disabled-состояние |

## Migration (ломающая, авторы — только мы)

| Сегодня | Цель |
|---------|------|
| Вывод математики размещения разлетается по вызовам на рендере | `VisualGraphBuilder` собирает его в граф один раз на ревизию; математика остаётся на своих местах, потребляется сборкой |
| `DashboardPageController`: first-tab/first-page/count-гейты/labels/fallback-ряд | Читает граф + сессию; гейты удаляются по одному, поведение сохраняется на каждом шаге |
| `Home.razor` TabBar/PagePicker с `Count > 1` | Полосы — узлы графа (рендер: существующие компоненты, далее `nav_tree`-плагин [ADR-0110]) |
| `ToolbarFilterVisibility` в цепочке | Спек-слоты + `Bound`; тесты флага вместо видимости |
| `PlacementGridCss` вычисляет | Проекция узла |
| Каталог: «первая запись» по умолчанию | Ошибка с записанным умолчанием |

## Implementation phases

| Phase | Deliverable |
|-------|-------------|
| **P0** | Типы `VisualGraph` (F#, `Modeling.Core`, словарь существующий) + `VisualGraphBuilder` (C#, `Execution.Runtime`, потребляет `LayoutSlotEngine.PlanCardInterior` + `CardInterior*`) над **интерьерами карточек**: сборка в bootstrap, дамп графа (conformance-fixture), headless-тесты без Blazor; рендер интерьеров читает граф |
| **P1** | Scope `TabBoard` + `PageToolbar`: билдер потребляет `TabLayout*`/`ToolbarLayoutCompactor`/fallback-ряды; хост читает граф; по одному убийству `RecomputeToolbarPlacements`/per-render резолвов, видимое поведение сохранено |
| **P2** | Scope `Shell` (зоны [ADR-0110]) + `NavBand` полосы + записанный `InitialRoute`; языковая добавка `page <id>` в каталоге; билдер потребляет `FilterPlacementAnalyzer` |
| **P3** | Экспорт для Studio (два графа, склеенные по Id, одним полотном) + визуальные conformance-экраны |

## Consequences

- Одна структура UI = один источник для Blazor, плагинов, Studio и headless-экспорта; воспроизводимость из артефакта.
- Существующее продолжает использоваться: математика — на своих местах (потребляется сборкой), словарь идентичности — один (`Id`/`WiringNodeKind`/`PlacementDefinition`); новый тип — только рекурсия Компоновщика.
- `DashboardPageController` худеет до обхода + сессии; Surface перестаёт быть «вторым автором».
- Визуальная итерация — правка декларации + дамп дерева: собрал, посмотрел композицию целиком.
- Два SSOT — данные (wiring-граф, [ADR-0091]) и структура (VisualGraph) — склеены по Id; никакого дублирования истины.
- [ADR-0110] обновлена на термин `VisualGraph`.
