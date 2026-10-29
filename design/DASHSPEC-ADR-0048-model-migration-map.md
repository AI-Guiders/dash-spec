---
type: ADR
id: DASHSPEC-ADR-0048
title: "Карта миграции модели (C -> только F#)"
status: "Working document"
date: 2026-10-17
tags: [dashspec, adr]
language: ru
---

# DASHSPEC-ADR-0048: Карта миграции модели (C -> только F#)

## Решение

**Цель:** модель и язык — только F# (`DashSpec.Modeling.*`). C# — исполнение и UI, читает F#-IR **напрямую**, своей модели не держит. Проект `DashSpec.Core` растворяется.

**Не цель:** перевод Execution/UI на F#. Razor (65 компонентов, 6.7k строк) в F# не компилируется, C#-кода 53k строк против 19k F#, и [ADR-0051](DASHSPEC-ADR-0051-language-affinity-modeling-execution.md) фиксирует границу «жёсткая модель → F#, механика → C#» как общую с федерацией.

## Механизм (проверен сборкой)

`global using`-алиас **перекрывает** одноимённый тип из `using DashSpec.Core.Model`:

```csharp
// model aliases в проекте-потребителе
global using FilterDefinition = DashSpec.Modeling.Parse.Filter.FilterDefinition;
global using FilterKind = DashSpec.Modeling.Parse.Filter.FilterKind;
```

Проверка (`DashSpec.Filters`, ветка): компилятор увидел `DashSpec.Modeling.Parse.Filter.FilterDefinition`, а на устаревшем `filter.Kind is not FilterKind.Top` ответил `CS9135` (значит `FilterKind` — F#-DU). Вывод: **тип подменяется одной строкой на проект, а компилятор выдаёт точный список мест под правку**.

Пока C#-модель ещё существует, алиасы позволяют вести миграцию волнами без «большого взрыва»: потребители постепенно начинают видеть F#-типы, а `src/DashSpec.Core/Model/*` в конце удаляется целиком.

## Инвентарь

Всего в `src/DashSpec.Core/Model/` **51 тип** (без служебных: `FilterKind`, `SqlDialect`, `DataSourceKind`, `DataSourceSqlCarrier`, `LayoutScope` — это F#-DU, см. ниже).

Инвентарь в этой карте составлен вручную по определениям типов и потребителям. Отдельные генераторы алиасов и отчётов удалены: миграция ведётся вручную, по типу и его потребителям, а проверяется компилятором.

| Группа | Типов | Что это значит |
|--------|-------|----------------|
| **Совпадают** | 33 | Имена и типы полей совпадают с F# (с точностью до `string?` ↔ `string option`). Правок структуры не нужно — только алиас и обработка `option`/DU на местах использования |
| **Расходятся** | 12 | Состав полей отличается — нужны точечные решения (см. ниже) |
| **Не модель** | 5 | Логика, остаётся в C# (`CardClickRefreshPlanner`, `VizAxisPresentation*`, `CategoryAxisOrder*`, `HeatmapCellContext`, `SeriesTransformSettings`, `DiagramKindInfo`, `DisplayBindingDefinition`, `DashflowExecutionPath`) |
| **Без F#-двойника** | 1 | `DashflowTransformerPortDefinition` — растворить в структуре F# `DataFlow` |

### 12 расхождений — конкретно

| C#-тип | Что расходится | Решение |
|--------|----------------|---------|
| `DashboardDocument` | C#: `RowTypeSchemas`; F#: `RowTypes`, `ReportScopeFlow`, `WiringGraph` | брать F#-форму; `RowTypeSchemas` — производное, убрать; `ReportScopeFlow`/`WiringGraph` нужны Studio — экспортировать как есть |
| `CardDefinition` | C#: —; F#: `CardInputs`, `InteriorFlow`; типы `DiagramSlots`, `FlowInput` | брать F#-форму (C#-модель просто недоэкспортировала IR) |
| `FilterDefinition` | F#: `ApplyMode` не переносился | брать F#-форму |
| `DataSourceDefinition` | C#: `ProviderId`, `ProviderInfer`; F#: `Provider` (DU `Infer \| Named of id`) | брать F#-форму, `ProviderInfer` → `Provider.IsInfer` |
| `CatalogGroupDefinition` | C#: `Title`; типы `Id` | разобрать вручную (вероятно, дубль/устаревшее) |
| `ReportPageDefinition` | F#: `ScopeFlow`; типы `DisplayBindings` | брать F#-форму |
| `DashflowModuleDefinition` | F#: `Diagnostics`, `Graph`, `NestedFlows`; типы `Links`, `Sources`, `Transformers` | взять F#-форму (C# — переупакованная проекция) |
| `DashflowSourceDefinition` | C#: `FromKind`, `FromValue`, `ProviderId`, `ProviderInfer`; F#: `From`, `Provider`, `DefaultOutputPort` | взять F#-форму |
| `DashflowTransformerDefinition` | F#: `Inputs`; типы `Outputs`, `Steps` | взять F#-форму |
| `DashflowTransformStepDefinition` | типы `Parameters` | взять F#-форму |
| `CardDiagramSlotDefinition` → `CardDiagramSlot` | типы `FlowInput` | взять F#-форму |
| `ModuleExtensionImport` | C#: `AssemblyPath`; типы `PluginId` | взять F#-форму |

### 13 типов **без `option`-полей** (структурно дешёвые)

`CardChromeDefinition`, `CardClickBehaviour`, `CardFlowInputDefinition`, `CardsChromeDefinition`, `DashflowTransformParameterDefinition`, `ExtensionBlockNode`, `FiltersChromeDefinition`, `HostLinkDefinition`, `LayoutBoardNestDefinition`, `LayoutDefinition`, `ModuleExtensionsDefinition`, `PaletteDefinition`, `PlacementDefinition`, `TooltipDefinition`.

Но «дешёвый по полям» ≠ «независимый»: волна должна быть **замкнута по зависимостям** (тип мигрируем вместе с типами своих полей). Поэтому реальные волны — ниже.

## DU-«энумы»: трение измерено

| F#-тип | Форма | Правок в C# |
|--------|-------|-------------|
| `CardFoldMode`, `CardVisibilityMode`, `CardsFoldPolicy`, `ShowPlacement`, `ShowFormat`, `ShowSource`, `CardBoundFilterChrome` | **настоящий .NET enum** | **0** — работают из C# как есть |
| `FilterKind` (148), `SqlDialect` (153), `DataSourceKind` (78), `DataSourceSqlCarrier` (15), `LayoutScope` (28) | **DU** | `x is not FilterKind.Top` → `!x.IsTop` — **≈ 422 места**, правки механические |
| `CardClickEffect` | DU с 8 case'ами; в C# — 8 отдельных record'ов + базовый | снять зеркала, перейти на `.IsShowSelection` / `as CardClickEffect.ShowSelection` |
| `LayoutBoardEntry` | DU с 3 case'ами; в C# — 3 record'а | то же |

## Главное трение — `option`

F# `string option` в C# — это `FSharpOption<string>`, **не** `null`-строка. `None` представлен как `null` (F# `UseNullAsTrueValue`), поэтому проверки `is null` работают, но присваивание в `string?` требует `.Value`.

Правило миграции: строку читаем как `filter.Label?.Value`, списки/словари — так же. Хелпер для повторяющихся мест:

```csharp
internal static class ModelOption
{
    public static T? OrNull<T>(this FSharpOption<T>? option) where T : class => option?.Value;
    public static T? OrNullValue<T>(this FSharpOption<T>? option) where T : struct => option?.Value;
}
```

## Ручная стратегия миграции

Генераторы удалены по решению оператора. Каждую группу мигрируем вручную: изучить F#-тип, найти всех C#-потребителей, добавить алиас именно этим проектам, удалить C#-объявление, собрать и прогнать тесты. Компилятор даёт точный список несовместимых мест.

Модель связана графом, но это не означает, что нужен один большой PR: **независимые листья можно мигрировать по одному**, как уже показали `HostLinkDefinition`, `LayoutDefinition` и другие. Корневые типы (`DashboardDocument`, `CardDefinition`, `FilterDefinition`) имеют большой fan-out и потребуют отдельной согласованной партии.

Таблица ниже — ручной инвентарь содержательных групп, а не план автоматически запускаемых волн.

| Волна | Что мигрирует | Объём |
|-------|----------------|-------|
| **W0** | Ручной список соответствий, решение по C#-only членам и тесты F#-IR | — |
| **W1** | Каталог и оболочка хоста: `CatalogDocument`, `CatalogEntryDefinition`, `CatalogGroupDefinition`, `HostDocument`, `HostLinkDefinition` | ~15 файлов |
| **W2** | Раскладка: `LayoutDefinition`, `PlacementDefinition`, `LayoutScope`, `LayoutBoardDefinition`, `LayoutBoardGroupDefinition`, `LayoutBoardNestDefinition`, `LayoutBoardEntry` | ~35 файлов |
| **W3** | Оформление: `PaletteDefinition`, `PresentationBlock`, `SeriesTransformBlock`, `CardChromeDefinition`, `CardsChromeDefinition`, `CardVisibilityRule`, `LegendDefinition`, `TooltipDefinition`, `InspectPresentation`, `MatrixRenderLimitsDefinition`, `ExtensionBlockNode`, `ModuleExtensionsDefinition`, `ModuleExtensionImport`, `ModuleDiagramDefinition` | ~40 файлов |
| **W4** | Фильтры: `FilterDefinition`, `FilterKind`, `FiltersChromeDefinition`, `FilterDeriveDefinition` | ~40 файлов |
| **W5** | Диаграмма и источник: `DiagramDefinition`, `DiagramDataFamily`, `DataSourceDefinition`, `DataSourceKind`, `DataSourceSqlCarrier` | ~30 файлов |
| **W6** | Клики: `CardClickBehaviour`, `CardClickEffect` (снять 8 зеркал) | ~20 файлов |
| **W7** | Dashflow: 8 типов `DataFlow` + `DashflowExecutionPath` | ~20 файлов |
| **W8** | Ядро: `DashboardDocument`, `CardDefinition`, `TabDefinition`, `ReportPageDefinition`, `ReportTimePolicy`, `ReportFormatDefaults`, `WorkCalendarDefinition`, `SqlDialect` | ~50 файлов |
| **W9** | Удаление `src/DashSpec.Core/Model/*` и `DocumentModelMapper` (маппинг больше не нужен) | — |
| **W10** | Перенос не-модельных частей `Core` в `Execution.*` (Layout 1525, Resolution 613, Analysis 529, Validation 616, Authoring 884, Runtime 565, Transforms 245 строк) и удаление проекта | 167 файлов |

Проверка каждой ручной партии: `dotnet build DashSpec.slnx` (предупреждения = ошибки), затем `dotnet test DashSpec.slnx` (база: 554 passed / 0 failed / 4 skipped). Не запускать build и test одновременно: у них общие `bin/obj`.

## Прогресс

| Шаг | Состояние |
|-----|-----------|
| **ADR-0103** — барьер «поверхности читают IR, но не разбирают» | ✅ сделано |
| **B** — члены модели перенесены в F# (`FilterDefinition.Is*`, `FiltersChromeDefinition.Is*`, `ReportTimePolicy.ClipsQueries`, `DashboardDocument.Resolved*`, `MatrixRenderLimitsDefinition.Effective*` / `IsOversized`) | ✅ сделано; прямые F#-тесты добавлены |
| **C** — не-модельный код вынесен из `Model/`: `VizAxisPresentation.cs` (133), `CategoryAxisOrder.cs` (102), `CardClickRefreshPlanner.cs` (37), `SeriesTransformSettings` → `Execution.Runtime` | ✅ сделано, коммит `733569e` |
| **C (остаток)** — `DescribeOversize` (русский текст) и `ToolbarDateLabelClass` (CSS) → presentation; `LayoutBoardDefinition.FromCardRows` | ✅ `DescribeOversize` → `DashSpec.Presentation.Matrix.MatrixOversizeDescription`; `ToolbarDateLabelClass` уже живёт в `FilterWidgetHost.razor` (CSS-решение в presentation, в F#-модель не переносилось); `FromCardRows` уже F# |
| **A** — удаление C#-дубликатов `Resolved*`/`Rows`/`RowCount` | ⏳ **нельзя до переключения**: C#-копии нужны текущим потребителям, удаляются внутри большой волны |
| **D** — большая волна: алиасы + удаление `Model/` + добить 400–600 мест | ✅ `src/DashSpec.Core/Model/` и `DocumentModelMapper.cs` удалены |
| **E** — ручная миграция независимых записей: предыдущие листья + `FilterDefinition`/`FilterKind`, `DataSourceDefinition`/kind/carrier, `HostDocument`, `ReportPageDefinition`, `CardDiagramSlotDefinition`, `CardDefinition`, `DiagramDataFamily`, `DiagramKindInfo`, `DashboardDocument`, `CardClickBehaviour` + effects, dashflow-записи | ✅ 40+ C#-типов удалено; `DocumentModelMapper` сведён к identity |

База на момент записи: сборка 0 warnings / 0 errors, тесты 554 passed / 0 failed.

Проверка партии 2026-10-17 (финал): `dotnet build DashSpec.slnx` — 0 warnings / 0 errors; `dotnet test` — 558 passed / 0 failed / 4 skipped. `src/DashSpec.Core/Model/` удалён целиком (включая `DashflowDefinitions.cs` и заглушки); `DocumentModelMapper.cs` удалён, вызовы инлайнены в `DocumentParseRegistration` (`document` напрямую). `HeatmapCellContext` — runtime DTO, а не spec-модель — переехал в `src/DashSpec.Core/Interaction/` (namespace `DashSpec.Core.Model` сохранён ради 240 потребителей). C# потребители читают F# DU через семантические члены (`TryGetFilterBind`, `IsNavigationEffect`, `Source`/`Format`/`Placement` и т.д.). Пункт **C (остаток)** закрыт 2026-10-17: `DescribeOversize` перенесён в `DashSpec.Presentation.Matrix.MatrixOversizeDescription` (потребители — viz-плагины `HeatmapView`/`MatrixCanvasView`); `ToolbarDateLabelClass` — CSS-решение, живёт в `FilterWidgetHost.razor` (`ToolbarLabeledCells ? "filter-inline-label" : "filter-label"`), в F#-модель сознательно не переносилось; `FromCardRows` — F# static member на `LayoutBoardDefinition`. Эпик закрыт: C#-модель и `DocumentModelMapper` удалены, F# — единственный источник модели.

## W10 — растворение Core (2026-10-20, волны на ветке `refactor/model-fsharp-ssot`)

Проверено боем: сборка 0/0 и тесты 558/0/4 после каждой волны; smoke на закрытых отчётных спеках (validate 7/9 OK, хост с живой БД, карточки рендерятся; 2 остальных — контент спеки, не код).

| Волна | Коммит | Что |
|---|---|---|
| W10.1 | `a293336` | Runtime-кодеки, IANA/offset-трансформы, cultures, `UtcOffsetZoneLiteral` → `Execution.Runtime` |
| W10.2 | `70fa252` | Layout-листья (15) → `Execution.Runtime`; замыкание Analysis/Resolution осталось |
| W10.3+4 | `e7b2c01` | Resolution+Analysis → `Execution.Runtime`, Validation → `Execution.Compilation` (разрез по направлению циклов) |
| W10.5+6 | `30e3efa` | Parsing (36) + Authoring (18) + stdlib → `Execution.Core`; компиляторный контракт + `DashSpecDiagnostic` → `Execution.Runtime`; заглушка `DashSpecParser` удалена |
| W10.7 | `113955f` | `XlsxWorkbook` → `Execution.Compilation`; `FilterLargeListOptions` → `Execution.Runtime`; `HeatmapCellContext` физически → `Execution.Runtime` (namespace-шрам `DashSpec.Core.Model` оставлен ради 240 потребителей) |
| W10.8 | `a957f3c` | **`DashSpec.Core` удалён.** `SpecLibrary` разрезан по I/O-шву: `SpecLibraryLoader` (TOML/File → `Execution.Core`) наполняет in-memory реестр `SpecLibrary` (→ `Execution.Runtime`, через internal-Register по IVT); I/O-семья без File (`SqlReadOnlyValidator`, `DashSpecParseException`, `DashSpecParseOptions`, `DiagramKindRegistry`) → `Execution.Runtime`; csproj + `ModelAliases` удалены, 17 ссылок снято, slnx почищен; Runtime ссылается на `Modeling.Parse` напрямую (раньше транзитивно через Core); слой-тест `Core_must_not_reference_AspNetCore` вышел на пенсию вместе со сборкой |

**W10 закрыт целиком (2026-10-20): `DashSpec.Core` не существует.** Сборка 0/0; тесты **557 passed / 0 failed / 4 skipped** (558 − пенсионер-тест). Финальный smoke — зелёный (хост, живая БД, карточки).

**Осталось (не W10, новые хвосты для следующих партий):** путь stdlib должен прийти из конфигурации (bootstrap/`.dashhost`-ключ), а не из хардкодов домов проектов (`UnitSource.fs`-кандидаты, `UnitBridgeHeaderTests`, `DashflowModuleParserTests` — последний уже переведён на `UnitSource.stdlibRoot()`); namespace-шрам `DashSpec.Core.Model` у `HeatmapCellContext` (240 потребителей, чистить отдельной волной); контент-баг одной закрытой спеки (блок `data flow` скопипащен дважды — ловится парсером как «more than one flow input», чинится в контенте спеки).

**Осталось (W10.8):** в Core по дизайну осталась только Parsing I/O-семья (`SpecLibrary`, `SqlReadOnlyValidator`, `DashSpecParseException`, `DashSpecParseOptions`, `DiagramKindRegistry`) + csproj + ModelAliases. DSPEC021 запрещает File I/O в `Execution.Runtime`, а Runtime не может ссылаться на `Execution.Core` — значит финал требует **разреза `SpecLibrary`** (TOML-загрузчик на стороне Host/Compilation, in-memory реестр пресетов → Runtime), затем удаления `DashSpec.Core.csproj` и снятия ссылок. Это отдельная содержательная правка, не механика.

**Долги, пойманные в W10 (не блокеры):** путь stdlib должен прийти из конфигурации (bootstrap/`.dashhost`-ключ), а не из хардкодов домов проектов (`UnitSource.fs`-кандидаты, `UnitBridgeHeaderTests`, `DashflowModuleParserTests` — последний уже переведён на `UnitSource.stdlibRoot()`); namespace-шрам `DashSpec.Core.Model` у `HeatmapCellContext`.

## Измеренный объём и два препятствия (проверено сборкой, 2026-10-16)

### Модель — один связный компонент

Типы вложены друг в друга (`DashboardDocument` → `CardDefinition` → `DiagramDefinition` / `DataSourceDefinition`, `HostDocument.TopbarLayout` → `LayoutBoardDefinition`, …), поэтому «волна по типу» невозможна: алиас перекрывает **имя**, а не идентичность типа, и частичная миграция даёт несоответствия на каждой границе. Мигрировать надо одним компонентом.

**Замер.** Алиасы поставлены в 13 проектах-потребителях (кроме самого `DashSpec.Core`), сборка `DashSpec.slnx`:

| Код ошибки | Значение | Штук |
|-----------|----------|------|
| `CS1503` | аргумент не того типа (`option` против `string`, C#-тип против F#-типа) | 136 |
| `CS9135` | `x is not FilterKind.Top` — это DU, нужен `.IsTop` | 50 |
| `CS1061` | нет члена: нужен `.Value`, либо на F#-типе нет C#--only члена (`IsDayWidget`, `RowCount`, `ResolvedCardsChrome`) | 46 |
| `CS8858`, `CS0029`, `CS0019`, `CS1929`, `CS1739`, `CS8602`, `CS0117`, `CS7036`, `CS8121` | прочее | 78 |
| **Итого** | | **310** |

Плюс собственные ошибки `DashSpec.Core`, который в замере не участвовал (он владелец модели). Реалистичная оценка полного объёма — **400–600 мест**.

Топ файлов: `FlowCardExecution.cs` (56), `TabLayoutCompactor.cs` (24), `GrainFilterPresentation.cs` (20), `DashflowPathResolver.cs` (20), `MatrixPresentation.cs` (18), `FilterDisplaySource.cs` (18).

Это был замер до первой ручной партии, а не прогноз оставшейся работы. Практика уже показала, что листья можно переключать отдельно с зелёной сборкой; объём миграции корневых типов будет измеряться по фактическим ошибкам компилятора.

### Препятствие 1 — снято (это была моя ошибка)

Ранее здесь было записано, что `src/DashSpec.Host/Generated/DashCatalog.g.cs` конструирует `CatalogDocument` и потому блокирует миграцию. **Это неверно** — вывод сделан grep'ом по имени и оказался ложным срабатыванием.

Проверка показала: в репозитории **два разных каталога** с похожими именами типов.

| | Каталог отчётов | Каталог команд федерации |
|---|---|---|
| Что это | Список разрешённых верхнеуровневых отчётов для зрителя ([ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md)) | Каталог команд и грамматик CommandPlane: slash-палитра `/select`, console, CCL (GUIDERS-ADR-0047) |
| Язык | `module catalog` (`.ds`), `samples/demo/demo-catalog.ds` | `catalog dash` (`.gdl`), `src/DashSpec.Host/Catalog/dash.catalog.gdl` |
| Тип | `DashSpec.Core.Model.CatalogDocument` — поля `Id`, `DefaultEntryId`, `Entries`, `Groups` | `AIGuiders.Platform.Authoring.Command.Catalog.CatalogDocument` — поля `Planet`, `Imports`, `Defaults`, `Channels` |
| Кто разбирает | F#-парсер DashSpec, в рантайме | внешний `gdlc` из соседнего репозитория `authoring-toolchain` |
| Генерация | нет, парсится на старте | `gdlc emit` → `src/DashSpec.Host/Generated/DashCatalog.g.cs` + drift-gate ([ADR-0052](DASHSPEC-ADR-0052-catalog-emit-drift-gate.md)) |

`DashCatalog.g.cs` использует **федеральный** тип (`using AIGuiders.Platform.Authoring.Command.Catalog;`, поля `Planet`/`Imports`/`Defaults`), а `CatalogEntryDefinition`/`CatalogGroupDefinition` там не встречаются вовсе.

**Следствие:** каталог отчётов мигрирует как любой другой тип модели; внешний генератор к этой миграции отношения не имеет. Блокирующих препятствий по каталогу нет.

### C#-члены модели: перенесённые и оставшиеся

Следующие простые члены уже перенесены в F#-записи (`FilterTypes.fs`, `DocumentTypes.fs`, `CardTypes.fs`): `FilterDefinition.Is*`, `FiltersChromeDefinition.Is*`, `ReportTimePolicy.ClipsQueries`, `DashboardDocument.Resolved*`, `MatrixRenderLimitsDefinition.Effective*` / `IsOversized`. **Они пока не являются production SSOT**, потому что C#-корневые типы ещё используются; поведение нужно покрыть F#-тестами до алиасирования этих корней.

Оставшиеся C#-члены, которые не относятся к данным модели:

| Где | Что | Куда это должно уйти |
|-----|-----|----------------------|
| `DashboardDocument` | часть `Resolved*` перенесена; `ResolvedModuleDiagrams`, `ResolvedChartChromePresets`, `ResolvedModuleTooltips`, `ResolvedRowTypeSchemas` + пустые словари | оставить F# extension/member; не переносить в C# execution |
| `FilterDefinition` | `IsDayWidget`, `IsComboboxWidget`, `IsSelectWidget`, `IsSingleSelectField`, `IsBindScopeInline/Hidden/Tooltip` уже продублированы в F# | покрыть F#-тестами; C#-дубликаты уйдут при алиасировании типа |
| `FiltersChromeDefinition` | предикаты уже продублированы в F#; `ToolbarDateLabelClass` (CSS) относится к UI | предикаты покрыть и убрать C#-копии; CSS оставить в Presentation |
| `LayoutBoardDefinition` | `RowCount`, `ColumnCount`, `Rows`, `FromCardRows` | extension-методы (часть уже есть в F#) |
| `MatrixRenderLimitsDefinition` | `EffectiveMaxCells/AxisLabels`, `IsOversized`, `DescribeOversize` | extension-методы / F# |
| `ReportTimePolicy` | `ClipsQueries` | extension-методы / F# |

Фабрики/парсеры/локализованный текст не переносить в F#-модель только ради совпадения API. Члены, являющиеся частью семантики IR, принадлежат F#; UI, разбор и execution-алгоритмы — своим слоям.

### Исполнимая процедура

1. Для каждого типа вручную сверить форму F#-IR, C#-потребителей и mapper.
2. Если тип независим: добавить алиас потребителям, удалить C#-объявление, собрать решение, устранить ошибки компилятора.
3. Перед корневыми типами добавить F#-тесты для перенесённых членов и проверить поля, которые mapper раньше терял.
4. После удаления модели разобрать и удалить `DocumentModelMapper`; затем переносить `Layout`/`Resolution`/`Analysis`/`Validation`/`Authoring`/`Runtime`/`Transforms` в `Execution.*` и растворять `Core`.

## Риски и что их снимает

| Риск | Снятие |
|------|--------|
| Ломается барьер `Host_must_not_reference_Modeling_Parse` | барьер задумывался против *второго парсера*; прямое чтение IR — это не разбор. Решение по барьеру — отдельная правка ADR-0076 + теста, **до** волн W1–W8 |
| Молча теряются поля IR (как сейчас: `RowTypes`, `WiringGraph`, `CardInputs`) | после каждой волны — тест-зеркало: состав полей F#-типа должен быть исчерпан списком «что читается» либо явным списком «сознательно не читается» |
| Массовые правки `option` дают регрессы | волны мелкие, каждый шаг — полный прогон тестов; поведение не меняется (типы те же) |
| `InternalsVisibleTo`/видимость при переезде | выдаются заново при переносе каждого блока W10 |

## Оценка

Старую оценку 15–20 человеко-дней отозвана: она опиралась на неверное предположение о единственной большой правке и не учитывала ручное прохождение зависимостей. Новая оценка будет дана после миграции корневых типов (`DashboardDocument`, `CardDefinition`, `FilterDefinition`) и замера количества ручных исправлений.
