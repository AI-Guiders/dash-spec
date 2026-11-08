---
type: Note
id: DASHSPEC-ADR-0126-MAP
title: "ADR-0126 план — db-transforms полного покрытия (window/union/join): карта работ"
status: "living"
date: 2026-11-08
tags: [dashspec, adr-0126, dashflow, implementation, plan]
language: ru
relates:
  - DASHSPEC-ADR-0125
  - DASHSPEC-ADR-0123
  - DASHSPEC-ADR-0121
---

# ADR-0126 план: db-transforms полного покрытия (window / union / join)

**Статус:** план согласован устно с оператором (2026-11-08), одна развилка открыта (§Открытые вопросы). Файл — рабочий документ; статус «living» до закрытия развилки и старта реализации.

## Context

ADR-0125 («Expressive power») зафиксировал: словарь db-трансформаций сегодня линеен и
одно-табличен (WHERE / GROUP BY+агрегаты / ORDER BY / LIMIT / проекция / DISTINCT), а
граница ручных view — функция покрытия примитивов. Цель: закрыть непокрытое — оконные
функции, UNION, JOIN (inner/left/right/full). Блокер: `DashflowExecutionPath` — линейная
цепочка, `QueryCompiler` компилирует один slot-запрос от одного source; join/union
(fan-in) требуют дерево. Решение оператора: сразу дерево и все три примитива.

**Коннектор (проверено по коду):** провайдер — атрибут source-блока флоу
(`use provider <id>|infer`, `DashflowProviderBridge`), infer = дефолт @runtime TOML;
коннекторы резолвит `RuntimeConnectorResolver` из @runtime-энтри, ровно один на
скомпилированный запрос. Cross-db не существует как случая — инвариант: провайдер
дерева = провайдер source-листа подключения карточки; несовпадение листьев — assert
на резолве.

## Зафиксированная разведка (ключевые точки)

| Слой | Где |
|---|---|
| Парсер флоу (F#) | `src/DashSpec.Modeling.Parse/DataFlow/DashflowModuleParser.fs`, `TransformStepParser.fs` — plugin-id шага свободная строка; multi-input transformer выразим (несколько `input stream` в ports). Узкое место — `readParameterValue` (хардкод per-plugin). |
| IR графа | `DataFlowGraph.fs` — `FlowNode{Kind,Inputs[],Outputs[]}` готов к fan-in; `FlowGraph.typeCheck` проверяет рёбра попарно. |
| Путь исполнения | `src/DashSpec.Execution.Runtime/DashflowExecutionPath.cs` — линейный record + `Invert()` (G⁻¹, только to_zone) + `ApplyToTime/ApplyToDateTime`. |
| Спеки шагов | образец `GroupByStepSpec.cs` (Find/Parse/Sql); белый список пушдауна — `OrderByStepSpec.CanPushDown` (позиционное правило ADR-0123 §5). |
| SQL | `QueryCompiler.cs` — ранний return group_by (:93-108), `BuildSelectSql` через `dialect.AppendSelect(SqlSelectParts)` (6 строковых полей; диалекты tsql/postgres/generic не трогаем). |
| Диспетчер runtime | `DashflowBatchExecutor.ApplyStep` — if-цепочка по PluginId. |
| Тесты | `QueryCompilerTests.cs` (спека-строкой → Compile → Assert SQL), `GroupByBranchTests.fs`, runtime-тесты в Core.Tests. |

## План работ (порядок = порядок коммитов)

### 0.5. Трансформы переезжают в Std.Transformations (словарь из стдлиба, без сидов)

Декларации трансформов — stdlib-юниты продукта (embedded-механика ADR-0017:
манифест + саморегистрация `StdlibEmbeddedInitializer`, видимость везде),
сгруппированные по локальности:

```
stdlib/transforms/database/summarize.ds   → Std.Transformations.Database
stdlib/transforms/database/order_by.ds    → Std.Transformations.Database (locality = any)
stdlib/transforms/runtime/to_zone.ds      → Std.Transformations.Runtime
stdlib/transforms/runtime/group.ds        → Std.Transformations.Runtime
stdlib/transforms/runtime/reduce.ds       → Std.Transformations.Runtime
```

- Словарь парсера = зарегистрированные декларации Std.Transformations.*; builtin-сидов
  нет — источник истины один. Неизвестный id = «не объявлен» (диагностика со списком).
- Декларация несёт: `locality = database|runtime|any`, параметры (ключ, тип литерала /
  enum значений, required). Расхождение локальности с подпространством (Database-юнит с
  `locality = runtime`) — линт-диагностика. `order_by` — единственный `any`
  (SQL-лицо первично, рантайм-сортировка — фолбэк).
- F#-валидаторы и батч-трансформы остаются реализацией; `checkStepLayering`,
  контракт литералов и словарь читают декларации (F# `knownPluginIds`/`localityOf`/
  `ParamContract` из среза 0 переходят в производное состояние / удаляются).
- Новые шаги 0126 (window/join/union) рождаются сразу декларациями в
  Std.Transformations.Database — словарь, локальность и параметры одним юнитом.

### 1. ADR-0126 (Proposed)
`design/DASHSPEC-ADR-0126-db-transforms-full-coverage.md`: window/union/join
(`kind = inner|left|right|full`); путь → дерево; послойный инвариант (срез 0) и
словарь Std.Transformations (срез 0.5) как фундамент; join/union/window —
database-only, объявлены в Std.Transformations.Database; один коннектор на запрос —
инвариант рантайма; инверсия G⁻¹ через fan-in не определена (задокументировать).
Индекс — `scripts/gen-adr-index.py`.

### 2. Дерево пути (рефакторинг ядра)
`DashflowExecutionPath` → дерево: `DashflowPathNode = Source(def) | Steps(transformer,
inputs: DashflowPathNode[])`. `DashflowPathResolver` собирает ветки fan-in (циклы —
громко). Assert: все Source-листья дают один провайдер. `Invert()/ApplyToTime/
ApplyToDateTime` обходят дерево; инверсия через fan-in-узел → NotSupportedException
с внятным текстом. Линейные пути — поведение байт-в-байт прежнее.

### 3. StepSpecs (по образцу GroupByStepSpec)
- **WindowStepSpec**: `func = running_sum|running_avg|running_count|rank|row_number`,
  `value`, `partition`, `order = col [asc|desc]`, `as = имя_колонки`;
  SQL: `AGG(val) OVER (PARTITION BY p ORDER BY o) AS x`.
- **JoinStepSpec**: `kind = inner|left|right|full` — эмит нативный
  `INNER/LEFT/RIGHT/FULL JOIN`, **без свапа веток** (владение фильтрами определяется
  портом, не позицией); `on = ключ` или `left_key`/`right_key`; алиасы l/r.
- **UnionStepSpec**: без параметров; канонический порядок колонок = поля output row-type.
- Локальность — инвариант послойности (см. закрытый вопрос 1): ParamContract
  дополняется `Locality = Database | Runtime`, порядок шагов проверяется после
  парсинга; window/join/union — database-only.

### 4. QueryCompiler: композиция веток (стандартный SQL:2003)
- **Параметры — скоп компиляции ветки**: каждая ветка компилируется со своим префиксом
  аллокатора (`@l_…`, `@r_…`); имена и значения рождаются сразу правильными, общий
  список — конкатенация. Никакого переименования готового текста.
- **Bound-фильтры — владение полем из row-type** (не зависит от вида join):
  - union: терминальный row-type одинаков для обеих веток (гарантия edge-typecheck) →
    предикат дублируется в каждую ветку до UNION ALL;
  - join: поле пришло ровно из одной ветки → предикат в ветку-владельца; одноимённые
    в обеих → квалификация по порту-владельцу (`l.field`/`r.field`; разбор dotted-имён
    частично есть в `SchemaContainsColumn`/`ResolveColumnName`).
- union: `(q1) UNION ALL (q2)`.
- join: `FROM (qL) l <KIND> JOIN (qR) r ON l.k = r.k`, селект-лист портов с квалификацией.
- window: обёртка `SELECT w.*, <OVER-expr> FROM (q) w` поверх итогового запроса
  (после group_by — семантика потока).

### 5. Runtime-лица
- `DashflowBatchExecutor.ApplyStep`: новых runtime-лиц нет (инвариант послойности —
  db-шаги не попадают в runtime-исполнение; нарушение порядка ловится раньше,
  на парсинге).
- `readParameterValue` — **сделано (b5421044)**: парсер не знает имён плагинов; каждый
  плагин декларирует parse-time контракт (`BuiltinScalarTransforms.paramContract`,
  нейтральные `ParamLiteral` + hint + RequiresParameterBlock). Новые шаги 0126 приносят
  свои контракты с собой; для `order = col asc` останется расширить generic-читатель
  (ident + опциональное направление тем же токеном строки).

### 6. DSL и типизация
Output row-type автор объявляет руками (существующая конвенция); edge-typecheck ловит
рассинхрон. Примеры DSL — в ADR-0126, стиль `GroupByBranchTests`.

### 7. Тесты
- Parse (F#): multi-input transformer; параметры join/window/union.
- QueryCompilerTests: `UNION ALL`, `INNER/LEFT/RIGHT/FULL JOIN … ON`,
  `OVER (PARTITION BY`; скопы параметров (`@l_`/`@r_`); фильтр дублируется в
  union-ветки / уходит в ветку-владельца join (для каждого kind); громкий случай
  (join после to_zone).
- Parse: порядок слоёв — db-шаг после runtime-шага = диагностика.
- Регресс: Core (530+) + Parse зелёные (известный красный `GetCompletions_use_flow` —
  pre-existing, вне скоупа).

### 8. Верификация
1. `dotnet build` хоста — 0 ошибок / 0 warnings.
2. `dotnet test` Core + Modeling.Parse — только pre-existing красный.
3. Коммиты по срезам (ADR → дерево → спеки+SQL → runtime → тесты);
   `gen-adr-index.py`; pre-commit gitleaks жив.

## Открытые вопросы

1. ~~Шаг после runtime-шага~~ — **закрыто оператором (2026-11-08): инвариант
   послойности.** Любые runtime-трансформации стоят ПОСЛЕ db-трансформаций:
   db-шаги — голова цепи (компилируются в SQL в порядке записи, в кадре
   источника/UTC), runtime-шаги — хвост (работают по батчу; to_zone —
   принципиально хвостовой, display-концерн). Следствия:
   - window/join/union — database-only, **без runtime-лица** (LINQ-двойник и
     тесты эквивалентности не нужны);
   - «смешение» исчезает как категория — есть только нарушение порядка =
     громкая диагностика (db-шаг после runtime-шага: «поставь шаг до to_zone»);
   - механика: `ParamContract` (b5421044) дополняется `Locality = Database |
     Runtime`; после парсинга — один проход по цепочке шагов с проверкой
     «Database не после Runtime». Живой корпус инварианту удовлетворяет
     (единственный transform-шаг LUS — group_by; to_zone в цепях не
     используется).
2. G⁻¹ через fan-in — подтвердить запрет (дрилл-клики через join-ветки не работают).

## Риски (принято осознанно)

- Инверсия G⁻¹ через fan-in запрещена (см. вопрос 2).
- `SqlSelectParts` не расширяется — композиция строками на уровне QueryCompiler,
  диалекты не трогаем.
