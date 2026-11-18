---
type: ADR
id: DASHSPEC-ADR-0132
title: "Иерархия конструктов — три уровня (ObjectConstruct → CollectionConstruct → MatrixConstruct) с единственными точками входа"
status: "Accepted"
implementation: "Реализован: Construct.normalizeObjectTableCells (Tier 1), Construct.projectAstWithLines + Declaration<'Row> (Tier 2), LayoutModuleParser.projectBoardFromAst/projectGridFromAst/projectTableRowsToLayoutRows + SlotContainer (Tier 3); потоковый LayoutParser и LayoutBracketRowParser удалены — все раскладки и сетки проходят через единую AST-проекцию Tier 3."
date: 2026-11-18
tags: [dashspec, adr, construct, grammar, ast, layout, surface-forms]
language: ru-RU
relates:
  - DASHSPEC-ADR-0106
  - DASHSPEC-ADR-0130
  - DASHSPEC-ADR-0131
  - DASHSPEC-ADR-0111
---

# DASHSPEC-ADR-0132: Иерархия конструктов — три уровня с единственными точками входа

## Context

ADR-0106 ввёл многоформенность (`ConstructForm`: `TableForm | BlockForm | LineForm | DottedForm`) для табличных конструктов, ADR-0130 перевёл парсеры на типизированное AST, ADR-0131 сделал `UnitGrammar`/`ConstructForm` контейнерной грамматикой. После этого одна и та же идея «объект можно записать несколькими поверхностями» начала независимо появляться в разных местах:

1. **Свойства блока** (`diagram`, `presentation`, `legend`, `inspect`, `limits`, `chrome`, `place`) — `PropertyBlockParser`/`BlockGrammar` принимали только `key = value`; табличную запись пришлось добавлять отдельным кодом.
2. **Поля типа строки** (`type <Name>`) — `TypeModuleParser` имел три раздельные ветки создания `RowFieldDef` (`TypeFieldDeclaration`, `PropertyAssignment`, таблица), каждая со своим разбором `optional`/`type`/`format`/`label`.
3. **Раскладки** (`module layout`, `card … layout`, `wide`/`mid`/`narrow`, `group`/`nest`, `vstack`/`hstack`) — пять эквивалентных поверхностей (скобочные строки, pipe-таблица, голые строки-слоты, `key = value` со списком, инлайн-стеки через `:`), из-за чего правила пришлось реализовывать дважды — в AST-проекции (`LayoutModuleParser`) и в потоковом парсере (`LayoutParser`).

Каждое новое место, где нужна многоформенность, заводило свой нормализатор. Это ровно тот класс дублирования, который ADR-0131 убрал для юнитов: две реализации одного правила расходятся молча, потому что тесты покрывают одну из них.

## Decision

Многоформенность языка раскладывается в **три уровня**, и каждый уровень имеет **ровно одну** точку входа. Уровень N строится только поверх уровня N−1; пересекаться уровням запрещено.

```text
Tier 3  MatrixConstruct   — рекурсивная 2D-плоскость слотов
        Rows × Columns, ячейка = имя слота [:вес] | [] (пустая ячейка)
        Slot.Inner : SlotContainer option — вложенная плоскость
        вход: LayoutModuleParser (AST-проекция board) / SlotPlaneBuilder

Tier 2  CollectionConstruct<'Row> — набор однотипных объектов
        вход: Construct.Declaration<'Row> + Construct.apply / projectAst / projectAstWithLines / applyTableRows
        формы: TableForm | BlockForm | DottedForm | LineForm

Tier 1  ObjectConstruct — атрибуты одного объекта
        вход: Construct.normalizeObjectTableCells
        формы: key = value | | key | value | (вертикальная) | | k1 | k2 | / | v1 | v2 | (горизонтальная запись)
```

### Tier 1 — `ObjectConstruct` (`Construct.normalizeObjectTableCells`)

Единственный нормализатор «таблица одной записи → список пар `key * value`». Распознаёт две раскладки таблицы:

- **вертикальная** — шапка `| key | value |` (или `| property | val |`), далее по паре на строку;
- **горизонтальная** — ровно две строки, где первая — имена ключей, вторая — значения; шапка распознаётся либо по длине > 2, либо по тому, что все ячейки первой строки входят в известный словарь ключей (`isKnownKey`).

Потребители Tier 1 обязаны делегировать, а не повторять разбор:
`Construct.fromAstBlock`, `BlockGrammar.applySchemaTableRows`, `PropertyBlockParser.projectPropertiesFromAst`.

### Tier 2 — `CollectionConstruct<'Row>` (`Construct.Declaration<'Row>`)

Один `Declaration<'Row>` (`Name`, `Vocabulary`, `CellsToRow`, `ConstructRow`) описывает конструкт, все поверхности нормализуются в карту атрибутов и проходят `UsedVocabulary → CellsToRow → ConstructRow`:

- `TableForm` — многострочная таблица; каждая строка превращается в карту Tier 1;
- `BlockForm` — дочерние блоки `<kind> <id> … end <kind>`; тело каждого блока читается Tier 1, поэтому внутри блока одинаково работают и `key = value`, и таблица;
- `DottedForm` — `<id>.<prop> = <value>`, группировка по `<id>`;
- `LineForm` — позиционные строки (`TypeFieldDeclaration`, `PortDeclaration`, `name = optional type`), нормализуемые в ту же карту.

Точки входа: `Construct.apply`, `Construct.projectAst`, `Construct.projectAstWithLines` (с необязательным проектором строчной формы), `Construct.applyTableRows`, `Construct.createTableConsumer`.

`projectAstWithLines` существует затем, чтобы доменный парсер мог добавить свою строчную поверхность **без** заведения второй ветки разбора: `TypeModuleParser` передаёт туда один `tryProjectFieldLine`, а не три `match`-ветки.

### Tier 3 — `MatrixConstruct` (`LayoutModuleParser` / `SlotPlane`)

Плоскость слотов рекурсивна: контейнер `Rows × Columns`, ячейка — либо имя слота с весом (`name:4`), либо пустая (`[]`), либо ссылка на вложенный контейнер. Пять поверхностей записи нормализуются в одни и те же `LayoutBoardEntry` до того, как их увидит сборщик:

`[ a b ]` · `| a | b |` · голые строки `a b` · `hstack = [ a, b ]` / `vstack = [ c ]` · `hstack: a b`

Рекурсия выражается данными, а не кодом: `code:Slot.Inner : SlotContainer option`; `vstack`/`hstack`/`group`/`nest`/`wide`/`mid`/`narrow` — синтаксис, а не отдельные рантайм-примитивы.

Ролевые токены (`@filters`, `@primary`) — тоже Tier 3: они разворачиваются в конкретные имена слотов до раскладки (`LayoutModuleParser.expandCardRoleTokens`), поэтому дальше по конвейеру роль уже не встречается.

## Invariants

1. Новый многоформенный конструкт добавляет **`Declaration<'Row>`**, а не новый парсер поверхности.
2. Новый блок свойств получает табличную запись **бесплатно** через `SchemaProperties` + Tier 1; отдельный код разбора таблицы в блоке — дефект.
3. Новая раскладка описывается через `LayoutBoardEntry`/`SlotContainer`; второй параллельный парсер раскладок — дефект.
4. Одна и та же поверхность не может давать разные результаты в разных местах: нормализация уровней 1–3 общая, доменными остаются только `CellsToRow`/`ConstructRow` и имена слотов.
5. Роль (`@filters`, `@primary`) существует только на входе Tier 3 и не протекает в `SlotPlane`.

## Consequences

+ Пять поверхностей раскладки, три поверхности типа строки, две поверхности свойств блока и четыре поверхности конструкта обслуживаются одним нормализатором на уровень — расхождение правил между местами структурно невозможно.
+ Плагин может принести свой юнит/конструкт, объявив `Declaration<'Row>`, и автоматически получить все поверхности, которые ядро уже умеет.
+ Ошибки типизированы одинаково: неизвестный ключ — `DashSpecUnknownPropertyException` из одной проверки `Vocabulary`, а не из каждой ветки.

− Лишний слой нормализации на горячем пути парсинга (несколько лишних списков пар на блок); измеренной проблемы нет, профиль не снимался.
− Tier 2 не умеет сам «угадывать» смысл ячеек: `CellsToRow` доменный, поэтому новая форма поверхности требует, чтобы домен выразил её в словаре `Vocabulary`.
