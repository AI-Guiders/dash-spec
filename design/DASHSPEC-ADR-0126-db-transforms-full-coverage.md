---
type: ADR
id: DASHSPEC-ADR-0126
title: "Composite sources — join/union как конструкторы источников, window как линейный db-шаг"
status: "Proposed"
implementation: "planned: composite source def + parser, provider/time_zone asserts, FROM-composition in QueryCompiler/ManagedViewsEmitter, WindowStepSpec (Std.Transformations.Database), drill G⁻¹ works over composite sources by construction"
date: 2026-11-09
tags: [dashspec, adr, dashflow, join, union, window, composite-source, adr-0120, adr-0125, adr-0127]
language: ru-RU
relates:
  - DASHSPEC-ADR-0119
  - DASHSPEC-ADR-0120
  - DASHSPEC-ADR-0125
  - DASHSPEC-ADR-0127
---

# DASHSPEC-ADR-0126: Composite sources — join/union как конструкторы источников, window как линейный db-шаг

## Context

ADR-0125 зафиксировал: словарь db-трансформаций линеен и одно-табличен
(WHERE / GROUP BY+агрегаты / ORDER BY / LIMIT / проекция / DISTINCT), а граница
ручных view — функция покрытия примитивов. Непокрытое: оконные функции, UNION,
JOIN (inner/left/right/full).

Рабочий план (design/DASHSPEC-ADR-0126-implementation-map.md, 2026-11-08) изначально
выбрал путь «сразу дерево»: `DashflowExecutionPath` превращается в дерево
`DashflowPathNode = Source | Steps(inputs[])`, join/union становятся fan-in
трансформерами, инверсия G⁻¹ через fan-in запрещается. Развилка вынесена в открытые
вопросы плана.

Разведка по коду (2026-11-09) уточнила картину:

- **Путь — это уже линейный одноправый спуск.** `DashflowPathResolver.walk`
  (`src/DashSpec.Modeling.Parse/DataFlow/DashflowPathResolver.fs:39-88`) идёт от входа
  карточки к source; при нескольких inbound-рёбрах трансформер сегодня **молча** берёт
  дефолтный порт или первый линк — fan-in на уровне трансформеров резолвером не
  поддерживается, а маскируется.
- **Drill идёт по G⁻¹ от диаграммы к источнику**
  (`src/DashSpec.Execution.Runtime/DashflowExecutionPath.cs:11-16`): форма пути —
  `(Source, Transformers[])`, ровно один source; инверсия определена как разворот
  цепочки (фактически to_zone с обратным сдвигом). «Шаг без обратной операции делает
  инверсию невозможной» — уже зафиксированный принцип.
- **Source — самодостаточная граница**: `DashflowSourceDef`
  (`src/DashSpec.Modeling.Parse/DataFlow/DashflowTypes.fs:22-30`) несёт provider
  (infer|named), from (view|sql|sql-file|xlsx), time_zone, output port и row-type.
  Всё, что ниже этой границы, инверсии не подлежит и не требует — дрилл попадает в
  тот же артефакт с фильтром по клику.

## Decision

**Join и union — конструкторы источников, не шаги.** Fan-in-трансформер не вводится,
дерево пути не вводится; window — обычный линейный db-шаг.

### 1. Composite source

`source`-блок получает вторую форму — композицию двух уже объявленных источников:

```
source activity_joined {
  from join activity_raw + users_raw
    kind = inner
    on = user_id            // или left_key = … / right_key = …
}

source all_activity {
  from union activity_a + activity_b
}
```

(точечный синтаксис фиксируется при реализации в стиле существующих key-value строк
source-блока; здесь — поверхность решения, не лексер)

Семантика:

- **Провайдер один**: `activity_joined` наследует провайдер обоих constituent'ов;
  несовпадение (включая infer vs named, резолвящихся в разных) — громкий assert на
  парсинге. Cross-db не существует как случай — инвариант ADR-0125 сохраняется.
- **time_zone один**: у обоих constituent'ов совпадает (или у обоих не заявлен).
- **Схема**: join — слияние схем с квалификацией `l.`/`r.` при конфликте имён
  (row-type composite синтезируется по правилам ADR-0127: типы портов выводятся из
  входных рёбер и алгебры композиции); union — идентичный терминальный row-type обеих
  веток (typecheck на объявлении), канонический порядок колонок = поля row-type.
- **Вложенность**: constituent — только простой source (view/sql). Композиция
  композиций и композиция с трансформером поверх ветки — не выразимы (v1-граница;
  расширяется позже отдельным решением в духе ADR-0125 «мощность = покрытие узлов»).
- **Типизация границы** (уточнение 2026-11-09): явный `rows` у source обязателен
  по определению — граница типизации нетипизированной не бывает, инференсу ADR-0127
  нужен seed. Парсер сегодня молча допускает пустой `OutputRowType`
  (`DashflowModuleParser.fs:28-138`) — добавляется валидация; живой корпус уже носит
  `ports { output stream raw: <Type> }` на source'ах. Вывод типа из базы — режим
  инструмента (`dsc infer rows`: учётка смотрит схему view/запроса и **эмитит**
  ports-блок в corpus), не среды компиляции: корпус самодостаточен, компиляция
  детерминирована и не зависит от доступа к БД; расхождение схемы базы с corpus
  ловит верификация (recommend-режим в духе ADR-0125), не тихая подмена типов.
  Composite source — единственный, чей тип компилятор выводит сам (алгебра
  композиции из типов constituent'ов, синтез RowTypeDef по ADR-0127); явная
  аннотация на нём — верифицируемый контракт. Join требует: колонка `on` существует
  по обе стороны, типы ключей сравнимы.

- **Авторская поверхность** (предложение Argon, 2026-11-09): объявление источника
  упрощается до `source <id>: <Type> { … }`. У source по определению нет input'ов и
  ровно один output (парсер уже запрещает иное), поэтому тип выводится из аннотации
  (заодно `rows` обязателен синтаксически), а порт-блок на source исчезает: имя
  единственного порта — украшение, ссылки на source в линках работают без
  порт-скобки. Легаси `ports { output stream raw: T }` на source транспилируется
  при чтении (прецедент ADR-0119 OQ1 — transpile legacy on read, без dual support);
  миграция корпуса механическая.

- **Словарь: `join` — только db** (по образцу order/sort-сплита). Join живёт ниже
  слоя трансформаций (конструктор источника) и в `Std.Transformations` декларации
  не имеет. Рантайм-склейка потоков, если понадобится, получает собственное имя
  в `Std.Transformations.Runtime` — кандидаты `merge` (симметричное слияние,
  аналог union) и `lookup` (обогащение по ключу из справочника); это отдельное
  решение про fan-in в `DashflowBatchExecutor`, G⁻¹ и QueryCompiler его не
  касаются (рантайм — за границей drill-инверсии). Смысл: слово `join` в DSL
  всегда означает SQL-склейку на источнике.

### 2. SQL-лицо и управляемые артефакты — без изменений по механике

`QueryCompiler` / `ManagedViewsEmitter` компилируют цепь от source как сейчас; меняется
только `ResolveFrom`: composite source даёт `FROM <l> l <KIND> JOIN <r> r ON l.k = r.k`
или `(SELECT … FROM l) UNION ALL (SELECT … FROM r)` с каноническим проекционным
списком. Flat VIEW, iTVF (ADR-0125 §3: bound-параметр → инлайнимая функция с фильтром
ДО агрегации), content-addressed имена — работают поверх composite source без модификаций:
цепь по-прежнему линейна, фильтр по-прежнему WHERE над FROM.

Bound-фильтры (ADR-0120 param-порты с ColumnBinding): колонка резолвится по схеме
composite source; неоднозначное имя (есть в обеих ветках) — квалификация по владельцу
или громкая диагностика, если владелец не определим схемой. Владение определяется
схемой источника, не позицией — свапа веток нет.

### 3. Window — линейный db-шаг

`window` — декларация в `Std.Transformations.Database` (тот же механизм, что
summarize/order): `type_effect = extend` (новая колонка), `locality = database`;
параметры `func = running_sum|running_avg|running_count|rank|row_number`, `value`,
`partition`, `order = col [asc|desc]`, `as`. SQL — обёртка
`SELECT w.*, <OVER-expr> FROM (<chain>) w` поверх скомпилированной цепи. Runtime-лица
нет — инвариант послойности (db-шаги — голова цепи) уже закрыт оператором (2026-11-08).

### 4. G⁻¹ и drill — решено конструктивно, запрета нет

Join/union стоят **ниже границы источника** — инвертировать их не требуется никогда:
G⁻¹ разворачивает только post-source цепочку трансформеров, как сегодня. Дрилл-клик по
диаграмме на composite source работает: клик уходит фильтром в тот же joined-артефакт.
Тем самым открытый вопрос 2 плана («подтвердить запрет G⁻¹ через fan-in») закрывается
не запретом, а устранением самого случая. Риск плана «инверсия G⁻¹ через fan-in
запрещена» снимается.

### 5. Пересмотр решения «сразу дерево» (2026-11-08)

Дерево `DashflowPathNode` и `DashflowPathResolver` сборки веток не реализуются —
мотивация (fan-in трансформеров) исчезла. `DashflowExecutionPath` остаётся линейным
record'ом; поведение байт-в-байт прежнее. Срезы плана 2 (дерево) и 4 в части скопов
`@l_`/`@r_` (веток нет — один аллокатор параметров) выпадают; срез 3 (спеки) заменяется
composite-source + WindowStepSpec.

### 6. Fan-in на трансформерах — не выразим, и не должен маскироваться

Сегодня `DashflowPathResolver.walk` при нескольких inbound-рёбрах молча выбирает
первый/дефолтный. Поскольку fan-in легитимен только как composite source, резолвер
получает громкую диагностику для трансформера с несколькими inbound-рёбрами от
**разных upstream-веток** (легаси-случай «несколько рёбер в один порт» из вложенных
флоу — отдельно проверить на живом корпусе перед включением; если корпус пользуется —
диагностика только при расхождении source-листьев).

## Consequences

- **+** Drill работает на joined/union-диаграммах (строже, чем вариант «запретить G⁻¹»).
- **+** Ядро не усложняется: ни дерева пути, ни скопированных параметров, ни свапа веток.
- **+** iTVF, managed views, фильтры-as-flow-nodes, type inference — переиспользуются как есть.
- **−** Join после трансформаций не выразим (агрегат по A к сырым B не приджойнить) —
  v1-граница, осознанно; корпус LUS таких паттернов не использует.
- **−** Composite-источник живёт в плоскости источника: его не видно как узел графа
  флоу (авторский UI показывает цепь от composite source как обычную цепь).
- Словарь Std.Transformations и ParamContract (b5421044) — без изменений; window
  приносит свой контракт с собой.

## План работ (порядок = порядок коммитов)

1. **Типы + парсер**: `DashflowSourceDef` + composite-форма (refs, kind, on);
   провайдер/time_zone/row-type проверки; диагностика fan-in по §6.
2. **SQL**: `ResolveFrom` — FROM-композиция в QueryCompiler/ManagedViewsEmitter;
   резолв bound-колонок по composite-схеме.
3. **WindowStepSpec**: декларация Std.Transformations.Database + spec + OVER-обёртка.
4. **Тесты**: компиляция `INNER/LEFT/RIGHT/FULL JOIN … ON`, `UNION ALL`; провайдер
   mismatch; неоднозначная bind-колонка; iTVF поверх composite; window OVER;
   Invert/ApplyToTime на composite-path; регресс Core + Parse.
5. `gen-adr-index.py`; implementation-map получает статус «реализуется по настоящему ADR».
