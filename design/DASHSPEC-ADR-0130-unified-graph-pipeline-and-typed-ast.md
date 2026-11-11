---
type: ADR
id: DASHSPEC-ADR-0130
title: "Единый графовый конвейер (parse → graph → IR) и типизированный AST членов"
status: "Accepted"
implementation: "Реализован: первоклассные ключевые слова всех блоков (Type, Source, Transformer, Ports, Transform, Palette, Tooltip, Host, Surfaces, Defaults), типизированные узлы членов (TypeFieldDeclaration, PropertyAssignment, PortDeclaration, FlowLinkDeclaration, позиционный ClauseDeclaration с единым SourceOriginSyntax, GridRowDeclaration, TableRowDeclaration), типизированная шапка BlockDeclaration (TypeAnnotation, Origin) и прямая проекция модулей (TypeModuleParser, DashflowModuleParser, LayoutModuleParser, PresentationModuleParser, TransformModuleParser) из AST без повторного TokenReader или вырезания substring."
date: 2026-11-10
tags: [dashspec, adr, ast, concept-graph, syntax, codecenter, parser, compiler]
language: ru-RU
relates:
  - DASHSPEC-ADR-0067
  - DASHSPEC-ADR-0091
  - DASHSPEC-ADR-0101
  - DASHSPEC-ADR-0128
---

# DASHSPEC-ADR-0130: Единый графовый конвейер (parse → graph → IR) и типизированный AST членов

## Context

По архитектуре языка (`ADR-0067` / `ADR-0091`) конвейер DashSpec задуман как
единый графовый пайплайн: **текст → парс в синтаксическое дерево / граф концептов →
работа компилятора и редактора с графом → проекция графа обратно в текст**.

Однако исторически в коде образовались две параллельные трубы:

1. **Поверхностное дерево и `DashSpecConceptGraph` (`DashSpecSurfaceSyntax` → `ParseTree` → `DashSpecConceptGraphBuilder`):**
   - Классификатор `BlockSurfaceLineClassifier` и `DashSpecBlockKeyword` знали только
     блоки дашборда (`card`, `section`, `page`, `phase`, `group`), тогда как блоки
     модульных юнитов (`type <id>`, `source <id>`, `transformer <id>`, `ports`,
     `transform use <id>`, `palette <id>`, `tooltip <id>`, `host`, `surfaces`, `defaults`)
     попадали в `Other` или вообще классифицировались как `Content`.
   - Всё содержимое внутри любого блока складывалось в безликий узел
     `DashSpecAstNode.Line { Tokens: SyntaxToken[] }`, который не нёс семантики членов.
2. **Императивные парсеры модулей (`TypeModuleParser`, `DashflowModuleParser`, `LayoutModuleParser`, …):**
   - Из-за того, что внутри `BlockDeclaration` лежали только сырые `Line`, модульные
     парсеры не читали `ParseTree`, а создавали второй `TokenReader` по сырой строке
     и заново разбирали текст циклами `while not reader.IsEof`.

## Decision

### 1. Полный словарь блочных контейнеров во всех видах модулей

`DashSpecBlockKeyword`, `DashSpecKeywords` и `BlockSurfaceLineClassifier` включают
все блочные конструкции языка как первоклассные кейсы:
- `Type` (`type <id> … end type`)
- `Source` (`source <id> [: RowType] [= Origin(…)] …`)
- `Transformer` (`transformer <id> { … }`)
- `Ports` (`ports … end ports`)
- `Transform` (`transform use <pluginId> { … }`)
- `Series` (`transform series … end series`)
- `Palette` (`palette [<id>] … end palette`)
- `Tooltip` (`tooltip [<id>] … end tooltip`)
- `Host` (`host … end host`)
- `Surfaces` (`surfaces … end surfaces`)
- `Defaults` (`defaults … end defaults`)

Узел `BlockDeclarationSyntax` несёт типизированную метаинформацию заголовка:
- `TypeAnnotation: string option` (опциональная аннотация `: RowType` на `source`)
- `Origin: SourceOriginSyntax option` (опциональный инициализатор `= Origin(…)` на `source`)

### 2. Типизированные узлы членов внутри блоков (6 грамматических семейств)

Вместо универсальной свалки `Line` для валидных конструкций языка вводятся
типизированные узлы `DashSpecAstNode` (и соответствующие `DashSpecConceptKind`
в `DashSpecConceptGraph`):

1. **`TypeFieldDeclaration of TypeFieldSyntax`** (внутри `type <id>`):
   - `Optional: bool`, `FieldType: DashType`, `FieldName: string`, `Tokens: SyntaxToken[]`.
2. **`PropertyAssignment of PropertySyntax`** (одно присваивание `<key> = <value>` на строку):
   - `Key: string`, `ValueText: string`, `KeyTokens: SyntaxToken[]`, `EqualsToken: SyntaxToken`, `ValueTokens: SyntaxToken[]`, `Tokens: SyntaxToken[]`.
3. **`ClauseDeclaration of ClauseSyntax`** с позиционным `ClauseKind` (никаких нетипизированных строковых свалок `ArgsText`):
   - `NamespaceClause of name: string` (`namespace <DottedName>`)
   - `ScopeClause of kind: string` (`scope toolbar|section|page|card|host`)
   - `UseClause of kind: string * target: string` (`use provider <id|infer>`, `use flow <id>`, …)
   - `FromClause of origin: SourceOriginSyntax` (`from view|sql|xlsx …` — разделяет единый `SourceOriginSyntax` с шапкой `source = Origin(…)`)
   - `DefaultPortClause of direction: string * portName: string` (`default input|output <port>`)
   - `DirectiveClause of keyword: string * args: string[]` (`rows`, `layout`, `catalog`, `entry`)
4. **`PortDeclaration of PortDeclarationSyntax`** (внутри `ports`):
   - `Role: PortDeclRole` (`PortInput | PortOutput | PortParam`), `PortShape`, `PortName`, `ValueType`, `DefaultValue`, `ColumnBinding`.
5. **`FlowLinkDeclaration of FlowLinkSyntax`** (рёбра `<from> [<out>] -> [<in>] <to>`).
6. **`GridRowDeclaration` и `TableRowDeclaration`** (строки сетки `[a:2] []` и строки таблиц `| a | b |`).

Узел `DashSpecAstNode.Line` остаётся для комментариев (trivia) и синтаксически
незавершённых строк при живом редактировании в Studio (graceful recovery, как
`IncompleteMemberSyntax` в Roslyn).

### 3. Единый конвейер: парс → граф → семантический IR / мутация → печать

- Модульные компиляторы (`TypeModuleParser`, `DashflowModuleParser`, `LayoutModuleParser`,
  `PresentationModuleParser`, `TransformModuleParser`, `PropertyBlockParser`) читают
  готовые типизированные узлы из дерева (`DashSpecSurfaceSyntax.parse`), без пересборки
  токенов (`flattenTokens` / `blockReader`) и без вырезания `substring` по `Span`.
- Генерация и структурное редактирование (`dsc generate types`, `DashSpecAstPrinter`)
  работают по загруженному в память AST (`synthesizeTypesUnit`, `mergeTypeIntoUnit`, `print`),
  без склейки строк и без повторного чтения файлов с диска.

## Consequences

+ Один проход лексера и построения `ParseTree`; модульные парсеры проецируют доменный IR
  напрямую из типизированных узлов `DashSpecAstNode`.
+ Единое представление источника `SourceOriginSyntax` для `source s = View(…)/InnerJoin(…)`
  и `from view/sql/xlsx`.
+ Правило языка: одно свойство `key = value` на строку делает строчную грамматику,
  форматтер и AST однозначными.
