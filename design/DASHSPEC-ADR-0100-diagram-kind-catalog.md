# DASHSPEC-ADR-0100: Diagram kind — вертикальная плагинная способность и каталог-композиция (SSOT)

| | |
|---|---|
| **Status** | Accepted · design fixed; implementation phased (P1–P5) |
| **Date** | 2026-10-09 |
| **Relates to** | [0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md) · [0008](DASHSPEC-ADR-0008-viz-render-plugins.md) · [0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md) · [0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) · [0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) · [0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md) · [0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md) · [0093](DASHSPEC-ADR-0093-qualified-flow-graph-kinds.md) · [0098](DASHSPEC-ADR-0098-compiler-import-namespaces.md) |

## Context

Факт об одном виде диаграммы сегодня размазан по четырём-пяти местам:

| Факт | Где живёт |
|------|-----------|
| kind id / keyword | F# `DiagramKindRegistry` (Parse), `DiagramKindContributorDescriptor.KindId` (Viewer), строки в RHS/IR |
| family | F# `DiagramDataFamily` (Parse), C# `DiagramDataFamily` (Core.Model), `DataFamily`-строка в дескрипторе, `IVizPlugin.DataFamily`-строка |
| topLimit | F# `DiagramKindSpec`, дескриптор, Core-фасад |
| роли/bindings | F# `PropertySchemas.chartDiagram` …, `BindingProperties` дескриптора, string-switch `DiagramBindings.SelectColumnRoles` |
| схема свойств | F# `PropertySchemas` |
| рендерер | `VizPluginRegistry.DefaultPluginIds` (family→viz, хардкод) + `render`-свойство |

При этом вид **открыт**: его регистрирует плагин (`IDashSpecContributorRegistry.AddDiagramKind` → `DashSpecContributorRegistry.DiagramKinds`, Viewer). «Built-in» — не отдельный тир, а стандартный набор плагинов, активируемый bundle-конфигом `[plugins]` в TOML. Значит:

- замкнутый тип вида невозможен; закрытые — только *оси* (family/archetype);
- вертикаль вида **разорвана**: kind+family+topLimit+роли → `diagram_builtin`; рендерер → отдельные `viz_*` (по family); роли/классификация → хардкод `DiagramBindings`.

В коде уже есть три идиомы композиции: **registry-of-definitions** (`FlowGraphKindRegistry`, `ImportKindRegistry`), **номинальный каталог** (`TypeCatalog = { Definitions: dict } + ofDefinitions + validate`) и **contributor aggregator** (`DashSpecContributorRegistry`). `TypeCatalog` — ближайший образец под «каталог = композиция».

## Decision

### 1. Принцип

Вид диаграммы — **вертикальная плагинная способность**. Один плагин объявляет полный срез вида. Нет тира «built-in»: стандартный набор — просто `[plugins]` в TOML.

### 2. Слои (вариант B)

```text
Abstractions   — плагинный контракт: DiagramKindContributorDescriptor + RegisterDiagramKind (loose, string на границе)
Modeling.Core  — типизированный DiagramKindCatalog/Definition + DiagramKindId (SSOT данных; F#)
Execution      — маппер плагинный дескриптор → Modeling-каталог; раздача в parse и потребителей
```

Плагины знают только `Abstractions`; `Modeling` остаётся базой без ссылок вверх; `Execution → Modeling`. Маппинг (и валидация family/archetype) — в Execution.

### 3. Форма каталога (Modeling.Core, F#)

```fsharp
type DiagramKindId = DiagramKindId of string                 // номинальный id, открытое множество
type DiagramFamily = Chart | Table | Scalar | Matrix | Gantt // закрытая ось
type DiagramArchetype = Category | Radial | Series | Matrix | Scalar | Table | Gantt
type BindingRole = { Role: string; Keys: string list; Required: bool }
type PropertySchema = { Name: string; ValueType: PropertyValueType }   // PropertyValueType переезжает в Core

type DiagramKindDefinition =
    { Id: DiagramKindId
      Family: DiagramFamily
      Archetype: DiagramArchetype
      SupportsTopLimit: bool
      Roles: BindingRole list
      Schema: PropertySchema list
      RendererId: string option }

type DiagramKindCatalog = { Definitions: IReadOnlyDictionary<DiagramKindId, DiagramKindDefinition> }

module DiagramKindCatalog =
    val ofDefinitions : seq<DiagramKindDefinition> -> DiagramKindCatalog   // дедуп id → ошибка
    val tryGet : DiagramKindCatalog -> DiagramKindId -> DiagramKindDefinition option
    val validate : DiagramKindCatalog -> Result<unit, string list>
```

Композиция — как `TypeCatalog`: словарь номинальных определений + `ofDefinitions` (дубликат → ошибка) + `validate`.

### 4. Плагинный контракт (Abstractions)

```csharp
public sealed record BindingRoleDescriptor(string Role, IReadOnlyList<string> Keys, bool Required = false);
public sealed record PropertySchemaDescriptor(string Name, string ValueType);

public sealed record DiagramKindContributorDescriptor(
    string PluginId,
    string KindId,
    string DataFamily,        // loose на границе; Execution валидирует → DiagramFamily
    string Archetype,
    bool SupportsTopLimit,
    IReadOnlyList<BindingRoleDescriptor> Roles,
    IReadOnlyList<PropertySchemaDescriptor> Schema,
    string? RendererId);

// на IDashSpecContributorRegistry:
void RegisterDiagramKind(DiagramKindContributorDescriptor descriptor);   // один RegisterX на вид (вертикаль)
```

`RegisterVizRenderer` остаётся для рендереров; вид **ссылается** на рендерер по id (`RendererId`), т.к. рендерер общий (все chart-виды → `viz_chartjs`).

### 5. Сборка и инъекция (Execution)

```text
TOML [plugins] active_bundle → plugins.register → DiagramKindContributorDescriptor[]
   → Execution mapper (validate family/archetype) → DiagramKindCatalog
       ├─► DashSpecParseOptions.DiagramKinds  → parse (id/schema/roles/family)
       └─► Execution consumers (bindings/render/validate)
```

`DashSpecParseOptions` расширяется полем `DiagramKinds: DiagramKindCatalog` (или порт-резолвером — см. Open Q3).

### 6. Потребители (после миграции)

- `DiagramBindings` — роли/архетип из `DiagramKindDefinition` (switch удаляется).
- `VizPluginRegistry` — `RendererId`, иначе family-фолбэк.
- `SpecLibrary` / валидация — по **эффективному** каталогу (не built-in only).
- IR `DiagramDefinition.Kind` — `DiagramKindId` (номинальный), не `string`.

### 7. Что сносится

- F# `DiagramKindRegistry` built-in specs; diagram-часть `PropertySchemas`.
- `DiagramBindings.SelectColumnRoles` / `IsCategoryChart` / `IsRadialChart` (string-switch).
- дубль `DiagramDataFamily` (F# Parse + C# Model) — один тип.
- `VizPluginRegistry.DefaultPluginIds` хардкод (family→viz).
- хардкоды стандартного набора: `ViewerPluginBootstrap.RegisterBuiltInPlugins` (код) + `DashSpecPluginLoader.ResolveBundlePluginIds` (ids) → только bundle-конфиг.
- upstream-слайс Core-DU из незакоммиченной работы (замкнутый `DiagramKind` в Modeling.Core) — несовместим с открытым множеством; снесён в Этапе 0 (2026-10-09).

### 8. Терминология (фиксируем)

- **slot** — слот сетки/лейаута (`CardDiagramSlot`, ADR-0072/0065). Не трогаем.
- **role** — binding-роль (`x`/`y`/`value`/`series`/…). Не путать со slot.

## Resolved decisions (2026-10-09, оператор)

| # | Вопрос | Решение |
|---|--------|---------|
| 1 | `PropertyValueType` (Scalar/String/DateRange/QualifiedName/CommaList/RestOfLine/ColumnBinding) в `Modeling.Core` | **Да** — переезжает в Core (schema живёт в каталоге) |
| 2 | Классификатор/форматтер | **Kind-agnostic** — kind-распознавание только в парсере по каталогу |
| 3 | Инъекция | **Поле `DashSpecParseOptions.DiagramKinds`** — отдельный порт не нужен |
| 4 | `PropertySchema.ValueType` в плагинном дескрипторе | **Строка (loose)** на границе; валидация в Execution (как `DataFamily`) |
| 5 | Always-active набор (`Scope`/`OnClick`/`viz*`/`filter*`/`CardViews`) | **Embedded TOML** — bundle-driven; default-бандл = embedded-ресурс: старт без внешнего файла, кодовых хардкодов нет |

**Примечание по `DiagramArchetype`:** оператор сомневается в необходимости оси («кажется не нужен»); оставлена как есть («сделаем как есть, потом поправим ещё раз»). Кандидат на удаление при следующем проходе правок. Удаление оси не задевает `Roles` — роли объявляются в `DiagramKindDefinition` явно.

## Migration phases

| Phase | Deliverable | Proof |
|-------|-------------|-------|
| **P1** | `Modeling.Core`: `DiagramKindId`/`DiagramFamily`/`DiagramArchetype`/`BindingRole`/`PropertySchema`/`DiagramKindDefinition`/`DiagramKindCatalog` (+ `ofDefinitions`/`validate`) | Core unit tests: дедуп, validate |
| **P2** | `Abstractions`: расширенный `DiagramKindContributorDescriptor` + `RegisterDiagramKind`; Execution-маппер плагин→`DiagramKindCatalog` (+валидация family/archetype) | маппер-тесты |
| **P3** | Parse потребляет каталог: `DashSpecParseOptions.DiagramKinds`; schema/roles/family из каталога; лексер kind-agnostic; удаление F# `DiagramKindRegistry` + diagram `PropertySchemas` | L1 `Modeling.Parse.Tests` (в т.ч. plugin-kind блок парсится) |
| **P4** | Execution-потребители из каталога: `DiagramBindings` (роли/архетип), `VizPluginRegistry` (RendererId), `SpecLibrary` (эффективный каталог); удаление switch + дублей `DiagramDataFamily` | L2 `Execution.Compilation.Tests`, Core.Tests |
| **P5** | IR `Kind` → `DiagramKindId`; bundle-driven стандартный набор (снять хардкоды); откат upstream Core-DU; тесты | L3 `Platform.Session.Tests`, Architecture, Host.Tests |

Порядок строгий: сначала каталог-данные (P1) и контракт (P2), затем parse (P3), затем execution (P4), затем IR/bundle (P5).

## Consequences

- Один источник факта о виде; снапшот вида = одна запись `DiagramKindDefinition`.
- Новый вид (включая plugin) = `RegisterDiagramKind` со всем срезом; `Modeling`/`Execution` не меняются.
- Вид становится данными на всём пути (parse → IR → execute → render); `DiagramKindId` в сигнатурах, сырые строки у краёв.
- Цена: `Schema`/`PropertyValueType` переезжают в Modeling.Core-данные; классификатор в парсере должен стать каталог-зависимым (или kind-agnostic).
- Риск: несинхронный маппинг family/archetype (закрытые оси) — лечится валидацией `validate` при сборке каталога.

## Summary

- Вид диаграммы = вертикальная плагинная способность; стандартный набор = `[plugins]`, не тир.
- SSOT — `DiagramKindCatalog` (Modeling.Core, форма `TypeCatalog`); плагинный контракт — в Abstractions; маппинг — в Execution (вариант **B**).
- Один `RegisterDiagramKind` объявляет id/family/archetype/topLimit/roles/schema/renderer.
- Сносятся: F# kind-регистр, diagram `PropertySchemas`, `DiagramBindings` switch, дубли `DiagramDataFamily`, family→viz хардкод, кодовые хардкоды bundle.
- Реализация фазами P1–P5; open questions закрыты 2026-10-09 (см. Resolved decisions).
