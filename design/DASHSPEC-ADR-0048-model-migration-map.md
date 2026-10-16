# DASHSPEC-ADR-0048 — карта миграции модели (C# → только F#)

| | |
|---|---|
| **Status** | Working document (ветка `refactor/model-fsharp-ssot`) |
| **Date** | 2026-10-16 |
| **Relates to** | [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) · [ADR-0051](DASHSPEC-ADR-0051-language-affinity-modeling-execution.md) · [ADR-0102](DASHSPEC-ADR-0102-one-language.md) |

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

Инструмент: `python tools/model-shape-diff.py` (сравнивает состав и типы полей C#-модели и F#-IR, `--json` — машинный отчёт).

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

## Волны — это порядок **содержания**, а не порядок исполнения

Порядок ниже — по замыканию зависимостей, от периферии к центру. Он полезен как чеклист «что смотреть», но **по отдельности волны неисполнимы**: см. следующий раздел.

| Волна | Что мигрирует | Объём |
|-------|----------------|-------|
| **W0** | Инфраструктура: генератор алиасов, `ModelOption`, `tools/model-shape-diff.py` | — |
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

Проверка каждой волны: `dotnet build DashSpec.slnx` (предупреждения = ошибки), `dotnet test DashSpec.slnx` (база: 554 passed / 0 failed / 4 skipped), точечно `DashSpec.Architecture.Tests` (барьер «Host не ссылается на `Modeling.Parse`» — при прямом чтении IR его придётся пересмотреть) и `DashSpec.Core.Tests`.

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

Вывод: объём **не катастрофический** (дни, не недели), но это **одна большая правка**, а не серия независимых волн.

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

### Препятствие 2 — C#-only члены на типах модели

На C#-модели висят члены, которых нет в F# («логика внутри модели»):

| Где | Что | Куда это должно уйти |
|-----|-----|----------------------|
| `DashboardDocument` | `ResolvedCardsChrome`, `ResolvedFormatDefaults`, `ResolvedTimePolicy`, `ResolvedCommandAliases`, `ResolvedModuleDiagrams`, `ResolvedChartChromePresets`, `ResolvedModuleTooltips`, `ResolvedRowTypeSchemas` + 5 статических пустышек | extension-методы в `Execution.*` |
| `FilterDefinition` | `IsDayWidget`, `IsComboboxWidget`, `IsSelectWidget`, `IsSingleSelectField`, `IsBindScopeInline/Hidden/Tooltip` | extension-методы в `Execution.*` |
| `FiltersChromeDefinition` | `IsBarLayout`, `IsSticky*`, `IsAutoApply`, `ToolbarDateLabelClass` и др. | extension-методы |
| `LayoutBoardDefinition` | `RowCount`, `ColumnCount`, `Rows`, `FromCardRows` | extension-методы (часть уже есть в F#) |
| `MatrixRenderLimitsDefinition` | `EffectiveMaxCells/AxisLabels`, `IsOversized`, `DescribeOversize` | extension-методы / F# |
| `ReportTimePolicy` | `ClipsQueries` | extension-методы / F# |

Это и есть настоящий «второй источник истины»: не данные, а **поведение**. Его надо вынести из модели до/во время миграции, иначе F#-тип его не отдаст.

### Исполнимая процедура

1. Вынести C#-only члены (§Препятствие 2) в extension-методы `Execution.*` — сборка остаётся зелёной, поведение не меняется.
2. Включить алиасы во всех потребителях **и** в `DashSpec.Core` (`python tools/gen-model-aliases.py --write`), удалить `src/DashSpec.Core/Model/*.cs`, дальше идти по списку `dotnet build` до нуля ошибок (ожидаемо 400–600 мест, из них ~200 механических).
3. Прогнать `dotnet test DashSpec.slnx` (база 554 passed), `dotnet test tests/DashSpec.Architecture.Tests` (правило ADR-0103).
4. Затем W10: перенос `Layout`/`Resolution`/`Analysis`/`Validation`/`Authoring`/`Runtime`/`Transforms` в `Execution.*` и удаление проекта.

## Риски и что их снимает

| Риск | Снятие |
|------|--------|
| Ломается барьер `Host_must_not_reference_Modeling_Parse` | барьер задумывался против *второго парсера*; прямое чтение IR — это не разбор. Решение по барьеру — отдельная правка ADR-0076 + теста, **до** волн W1–W8 |
| Молча теряются поля IR (как сейчас: `RowTypes`, `WiringGraph`, `CardInputs`) | после каждой волны — тест-зеркало: состав полей F#-типа должен быть исчерпан списком «что читается» либо явным списком «сознательно не читается» |
| Массовые правки `option` дают регрессы | волны мелкие, каждый шаг — полный прогон тестов; поведение не меняется (типы те же) |
| `InternalsVisibleTo`/видимость при переезде | выдаются заново при переносе каждого блока W10 |

## Оценка

- Модель (W1–W9): **3–5 дней**.
- Растворение `Core` (W10): **10–15 дней**.
- Итого: **15–20 человеко-дней**, 10 волн, каждая заканчивается зелёной сборкой.
