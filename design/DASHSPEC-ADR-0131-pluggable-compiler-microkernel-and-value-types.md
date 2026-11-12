---
type: ADR
id: DASHSPEC-ADR-0131
title: "Плагинное микроядро компилятора, единая контейнерная грамматика (UnitGrammar / ConstructForm), доменные ValueTypes и самодостаточные правила"
status: "Accepted"
implementation: "Реализован: UnitGrammar (ContainerView + UnitEnvelope), UnitProjector (открытый реестр UnitPluginDescriptor) + stdlib/units/units.ds, ConstructForm (TableForm | BlockForm | LineForm) + TableRowKind + BlockGrammar.TableConstruct, индуктивный DotSyntax, FlowEndpointSyntax (FlowEndpointTarget + PortSelectorSyntax), PropertyValueSyntax с валидированными ValueTypes (HexColorValue, TimeShiftValue, IanaZoneValue, RelativeDayValue, DateBoundValue, DateRangeValue), многострочные списки [ … ], самодостаточный DashSpecRuleDescriptor со специфичными классами исключений + stdlib/rules/rules.ds + плагинные чекеры (AddRuleChecker, AddUnitKind), удалены 8 мёртвых *ParseBridge.cs и UnitSource.blankHeader."
date: 2026-11-12
tags: [dashspec, adr, compiler, microkernel, ast, unit-grammar, construct, value-types, rules, plugins]
language: ru-RU
relates:
  - DASHSPEC-ADR-0067
  - DASHSPEC-ADR-0106
  - DASHSPEC-ADR-0128
  - DASHSPEC-ADR-0130
---

# DASHSPEC-ADR-0131: Плагинное микроядро компилятора, единая контейнерная грамматика и доменные ValueTypes

## Context

После перевода компилятора на единое синтаксическое дерево (`DASHSPEC-ADR-0130`) в архитектуре оставались четыре рудимента до-AST эпохи:

1. **Разделение «грамматики модулей» и «грамматики блоков» + зоопарк `*ModuleParser`:**
   - Модули верхнего уровня (`module <kind> <id>`) и вложенные блоки (`<block> [<id>] … end <block>`) обрабатывались разными механизмами, хотя в языке они имеют изоморфную контейнерную структуру.
   - В `DashSpec.Execution.Core/Parsing` жили 8 промежуточных файлов `*ParseBridge.cs`, перекидывающих статические делегаты внутри одной и той же сборки, а `UnitSource.readUnit` затирал строки `namespace` и `import` пробелами (`blankHeader`).
2. **Половинчатость мультиформных конструкций (`Construct`):**
   - `Construct.fs` обрабатывал только сырые строки таблиц (`string list`), тогда как блочные и строчные формы тех же объектов (`links`, `when`, `filters`) разбирались отдельным кодом, а `TableRowSyntax` в AST хранил безликий `string[]`.
3. **Нетипизированные правые части присваиваний и концы стрелок:**
   - Квалифицированные имена с точкой (`a.b.c`), правые части `key = value` (цвета `#rgb`, таймзоны `UTC+3` / `Europe/Moscow`, сдвиги `-7d`, диапазоны `-7d..today`, вызовы `color(...)`, списки `[...]`) и концы потоковых стрелок (`producer [out] -> [in] consumer`) хранились в AST как плоские строки.
4. **Разрозненная валидация и плоский `DashSpecParseException`:**
   - Все ошибки парсинга бросались одним классом `DashSpecParseException(string)` без привязки к правилу (`DashSpecRule`), а виды модулей и правила перечислялись в захардкоженных `switch`/`match` по всему компилятору.

## Decision

### 1. Фиксированный каркас ядра — открытый плагинный словарь (`UnitGrammar` + `UnitProjector` + `stdlib/units/units.ds`)

Компилятор строится по тому же принципу микроядра, что и рендер-пайплайн:
- **Лексер и поверхностное дерево (`DashSpecLexer` + `DashSpecSurfaceSyntax`)** фиксируют только универсальный каркас языка (пунктуация `=`, `.`, `..`, `->`, `|`, `[ ]`, `( )`, `{ }`, контейнеры `module` и `block .. end`).
- **`UnitGrammar`** даёт единое представление `ContainerView` (`Properties`, `Clauses`, `Blocks`, `GridRows`, `TableRows`, `FlowLinks`, `TypeFields`, `Ports`, `Body`) для любого контейнера (как юнита `ModuleDeclaration`, так и блока `BlockDeclaration`) и `unwrapOptionalBlock` для разворачивания одноимённых внутренних блоков.
- **Декларативный манифест юнитов `stdlib/units/units.ds`** (`| unit | import_as | role |`, `EmbeddedResource`) задаёт допустимые виды юнитов, их ключевые слова импорта во множественном числе (`import transforms`, `import layouts`, …) и фазу сборки (`declaration | consumer | root`).
- **`UnitProjector`** — чистое микроядро над `UnitGrammar`, не зависящее от конкретных доменов: каждая вертикаль (`Types`, `Flow`, `Layout`, `Presentation`, `Transform`, `Palette`, `Tooltip`, `Catalog`, `Host`, `Diagram`, `Section`, `Dashboard` и внешние плагины через `IDashSpecContributorRegistry.AddUnitKind`) сама регистрирует свой `UnitPluginDescriptor`.
- Костыль `UnitSource.blankHeader` и 8 транзитных файлов `*ParseBridge.cs` удалены.

### 2. Изоморфизм форм (`ConstructForm`: `TableForm | BlockForm | LineForm`) и типизированный `TableRowKind`

- В `Construct.fs` введён алгебраический тип поверхности конструкта:
  ```fsharp
  type ConstructForm =
      | TableForm of rows: string[][]
      | BlockForm of headerBindings: (string * string) seq * properties: seq<KeyValuePair<string, string>>
      | LineForm  of bindings: (string * string) seq
  ```
  Все три формы нормализуются в единую карту атрибутов и проходят через один и тот же `Declaration<'Row>` (`Vocabulary` → `CellsToRow` → `ConstructRow`).
- В `BlockGrammar` удалён строковый буфер `TableRowRows`; введён потоковый `TableConstruct of Construct.TableConsumer`.
- В AST узел `TableRowSyntax` несёт доменный `Kind: TableRowKind` (`Header`, `Separator`, `Filter of FilterRowSyntax`, `Link of LinkRowSyntax`, `When of WhenRowSyntax`, `Click of ClickRowSyntax`, `Column of ColumnRowSyntax`, `Generic`).

### 3. Первоклассные операторы AST (`DotSyntax`, `FlowEndpointSyntax`, `PropertyValueSyntax`) и доменные `ValueTypes`

1. **Индуктивный оператор точки `DotSyntax`:**
   - `DotSyntax = Ident of name: string | Member of target: DotSyntax * memberName: string` (`.Root`, `.MemberName`, `.Segments`, `.IsQualified`, `.Text`).
2. **Типизированная стрелочная грамматика `FlowEndpointSyntax`:**
   - Каждый конец `FlowLinkSyntax` (`Source -> Target`) типизирован как `FlowEndpointSyntax = { Target: FlowEndpointTarget; Path: DotSyntax; Port: PortSelectorSyntax option }`, где `FlowEndpointTarget` — закрытый DU (`ImplicitDiagram`, `ReportAnchor`, `PageAnchor`, `CardAnchor`, `CardSlot`, `DashboardChrome`, `SectionChrome`, `CardChrome`, `HostTarget`, `PhaseTarget`, `FilterNode`, `Node`).
3. **Валидированные доменные `ValueTypes` (`DashSpec.Modeling.Core/ValueTypes.fs`) в `PropertyValueSyntax`:**
   - `HexColorValue` (`#rgb` / `#rrggbb` с нормализацией и байтами `R, G, B`),
   - `TimeShiftValue` (`UTC`, `UTC±hh:mm` с проверкой диапазона `±14:00` и `TotalMinutes`),
   - `IanaZoneValue` (валидированный IANA идентификатор),
   - `RelativeDayValue` (`-Nd`),
   - `DateBoundValue` (`Today | Relative | Calendar`) и `DateRangeValue` (`From..To`),
   - плюс `Boolean`, `Integer`, `Decimal`, `StringLiteral`, `Member of DotSyntax`, `Range`, `Call`, `BracketList` (включая многострочные списки `[\n … \n]`), `CommaList`, `Aliased`.
4. **Единообразие `=` в декларациях трансформаций (`stdlib/transforms/**/*.ds`):**
   - Параметры трансформаций записываются стандартным присваиванием `param <name> = <kind>` (аналогично `const <name> = <color>` в палитрах).

### 4. Самодостаточные правила (`DashSpecRuleDescriptor`), типизированные исключения и плагинные чекеры

- Каждое правило в `DashSpecRules` оформлено как самодостаточный `DashSpecRuleDescriptor` (`Id`, `Rule`, `Code`, `Severity`, `Title`, `CreateException`), а `DashSpecParseException` требует обязательный `descriptor: DashSpecRuleDescriptor` (без `option`).
- Введены специфичные подклассы исключений по категориям правил (`DashSpecBlockBalanceException`, `DashSpecUnclosedTokenException`, `DashSpecUnexpectedTokenException`, `DashSpecUnitHeaderException`, `DashSpecUnexpectedStatementException`, `DashSpecDuplicateDeclarationException`, `DashSpecMissingMemberException`, `DashSpecUnknownPropertyException`, `DashSpecInvalidValueException`, `DashSpecTableStructureException`, `DashSpecScopeMismatchException`, `DashSpecUnresolvedReferenceException`, `DashSpecTypeMismatchException`).
- Активные чекеры и их уровни серьёзности декларируются во встроенном манифесте `stdlib/rules/rules.ds` (`| checker | severity |`), а плагины регистрируют собственные чекеры через `IDashSpecContributorRegistry.AddRuleChecker` и `DashSpecRuleChecks.registerChecker`.

## Consequences

+ Полная вертикальность плагинов: плагин может принести вид юнита, вид диаграммы, блок расширения, трансформацию и чекер правил без правок ядра компилятора.
+ Никакого повторного лексинга или затирания шапок пробелами (`blankHeader`); все 10 модульных юнитов проецируются напрямую из единого AST.
+ Каждая синтаксическая конструкция языка (`DotSyntax`, `PropertyValueSyntax`, `ValueTypes`, `FlowEndpointSyntax`, `TableRowKind`, `ConstructForm`) представлена строгим алгебраическим типом F#.
