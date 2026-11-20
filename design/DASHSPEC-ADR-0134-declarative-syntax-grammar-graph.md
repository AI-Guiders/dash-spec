---
type: ADR
id: DASHSPEC-ADR-0134
title: "Декларативный граф грамматики синтаксиса (DashSpecGrammarGraph) — единый автомат вложенности контейнеров и цепочек токенов"
status: "Accepted"
implementation: "Реализован в src/DashSpec.Modeling.Parse/Syntax/DashSpecGrammarGraph.fs и подключён в DashSpecEditorIntelligence.cs и DashSpecCompletion.fs: GrammarScope (Root / Module / Block), GrammarExpectation (Keyword / PropertyKey / ClosingKeyword / UnitKind / ImportKind / TransformPlugin / DiagramKind / SymbolRef / ValueDomain), декларативные таблицы containerStarters и tokenTransitions, функция DashSpecGrammarGraph.expectedAt (ParseTree -> offset -> GrammarExpectation list). Ветвящаяся императивная логика из DashSpecEditorIntelligence удалена."
date: 2026-11-19
tags: [dashspec, adr, grammar, graph, ast, intellisense, lsp, syntax]
language: ru-RU
relates:
  - DASHSPEC-ADR-0130
  - DASHSPEC-ADR-0131
  - DASHSPEC-ADR-0132
  - DASHSPEC-ADR-0133
---

# DASHSPEC-ADR-0134: Декларативный граф грамматики синтаксиса (`DashSpecGrammarGraph`)

## Context

После перевода компилятора на типизированное дерево `ParseTree` (`DashSpecAstNode`, ADR-0130), микроядро юнитов (`UnitGrammar` / `UnitProjector`, ADR-0131) и трёхуровневую иерархию конструктов (`Construct`, ADR-0132) словари отдельных сущностей уже живут в декларативных реестрах:
- виды юнитов и импортов — `stdlib/units/units.ds` (`ImportKindRegistry`);
- трансформы и их параметры — `stdlib/transforms/**.ds` (`TransformDeclarations`);
- виды диаграмм и их схемы — `DiagramKindRegistry`;
- схемы свойств блоков — `PropertySchemas`.

Однако **правила следования («какой блок или клауза допустимы внутри какого контейнера» и «какой токен грамматически ожидается после какого токена в строке»)** оставались закодированными императивными `if/else`-ветками в `DashSpecEditorIntelligence.cs` и `DashSpecCompletion.fs`.

В языке DashSpec в каждой позиции (кроме свободного выбора идентификатора при объявлении) множество допустимых следующих токенов **строго детерминировано** текущим контейнером (`GrammarScope`) и префиксом токенов текущей строки. Императивные `if`-проверки приводили к рассинхронизации с реальными парсерами (например, предложение `transform <plugin>` без обязательного `use` внутри `transformer`, или предложение карточек вне блока `report`).

## Decision

В слое `DashSpec.Modeling.Parse.Syntax` вводится декларативный граф переходов грамматики **`DashSpecGrammarGraph`**:

### 1. Узлы области видимости (`GrammarScope`)

Позиция в `ParseTree` однозначно определяет стек активных контейнеров (`DashSpecAstNode.ModuleDeclaration` и `DashSpecAstNode.BlockDeclaration`, чей `HeaderSpan.End < caretOffset`):

```fsharp
[<RequireQualifiedAccess>]
type GrammarScope =
    | Root
    | Module of kind: DashSpecModuleKind
    | Block of keyword: DashSpecBlockKeyword * parent: GrammarScope
```

### 2. Типизированные ожидания (`GrammarExpectation`)

Каждое исходящее ребро графа из состояния `(GrammarScope, PrefixTokens)` указывает на типизированный класс ожидаемого токена:

```fsharp
[<RequireQualifiedAccess>]
type GrammarSymbolKind =
    | UnitOfKind of unitKeyword: string
    | Namespace
    | RowType
    | DeclaredFilter
    | DeclaredCard
    | DeclaredFlowNode
    | DeclaredPort of role: PortDeclRole

[<RequireQualifiedAccess>]
type GrammarExpectation =
    | Keyword of word: string * detail: string
    | PropertyKey of spec: PropertySchemas.PropertySpec * context: string
    | ClosingKeyword of word: string
    | UnitKindKeyword
    | ImportKindKeyword
    | TransformPluginId
    | DiagramKindId
    | SymbolRef of kind: GrammarSymbolKind
    | ValueDomain of propertyKey: string * values: string list
```

### 3. Два слоя рёбер графа

1. **Мапа контейнеров (`containerRules : GrammarScope -> GrammarExpectation list`):**
   Определяет, какие открывающие ключевые слова блоков, клаузы, схемы `PropertySchemas` и символы могут начинать строку внутри данного `GrammarScope`.
2. **Граф переходов внутри строки (`lineTransitions : GrammarScope -> SyntaxToken list -> GrammarExpectation list`):**
   По уже набранным на текущей строке типизированным токенам (`SyntaxToken`) детерминированно возвращает следующие ожидаемые токены:
   - `[module]` $\to$ `UnitKindKeyword`
   - `[end]` $\to$ `ClosingKeyword` активного контейнера
   - `[import]` $\to$ `ImportKindKeyword` $\to$ `Keyword "from"` $\to$ `SymbolRef Namespace`
   - `(Block Ports, [default])` $\to$ `Keyword "input" | Keyword "output"` $\to$ `SymbolRef (DeclaredPort role)`
   - `(Block Ports, [input])` $\to$ `Keyword "stream" | Keyword "scalar"` $\to$ `<ident>` $\to$ `:` $\to$ `SymbolRef RowType`
   - `(Block Ports, [output])` $\to$ `Keyword "stream"` $\to$ `<ident>` $\to$ `:` $\to$ `SymbolRef RowType`
   - `(Block Transformer, [transform])` $\to$ `Keyword "use"` $\to$ `TransformPluginId`
   - `(Block Filter, [bind])` $\to$ `Keyword "field" | Keyword "date" | Keyword "top"`
   - `(Block Connect, [use])` $\to$ `Keyword "palette" | Keyword "flow"` $\to$ `SymbolRef (UnitOfKind ...)`
   - `(Block Source, [use])` $\to$ `Keyword "provider"` $\to$ `ValueDomain("provider", ...)`

### 4. Роль `DashSpecEditorIntelligence` и `DashSpecCompletion`

`DashSpecEditorIntelligence` больше не содержит грамматических `if`-веток: он вызывает `DashSpecGrammarGraph.expectedAt tree caretOffset` и отображает полученные `GrammarExpectation` в `CompletionSuggestion`, разрешая `SymbolRef` через `DashSpecWorkspaceIndex` / `UnitIndex`.

## Consequences

- **Единый источник правды о структуре языка в `DashSpec.Modeling.Parse.Syntax`:** переходы между контейнерами и токенами заданы декларативно в одном F#-модуле рядом с `DashSpecAstNodes` и `PropertySchemas`.
- **Детерминированный IntelliSense:** в любой точке файла предлагаются только те одиночные токены, которые реально принимает парсер в данном `GrammarScope`.
- **Простота C#-обвязки:** `DashSpecEditorIntelligence.cs` становится тонким проектором `GrammarExpectation -> CompletionSuggestion`.
