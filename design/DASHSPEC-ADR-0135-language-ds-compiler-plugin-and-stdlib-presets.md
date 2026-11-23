---
type: ADR
id: DASHSPEC-ADR-0135
title: "Плагин компилятора языка (language_ds / IReportCompiler), стандартные презентационные пресеты в stdlib и отказ от нетипизированных дефолтов"
status: "Accepted"
implementation: "Реализован в DashSpec.Compiler.Contract (IReportCompiler), DashSpecLanguageBuiltinPlugin (id = language_ds), DashSpecContributorRegistry (авто-обнаружение embedded stdlib/**/*.ds из плагинов), DashSpecParseOptionsProvider (AST-извлечение ключей презентаций без C#-массивов строк), stdlib/presentation/table_compact.ds и table_detail.ds (namespace DashSpec.Std), ClauseKind.UsePresetClause и типизированных DU TableColumnSize / TableColumnAlign / TableSortDirection / DashSpecBlockKeyword. Защищено архитектурным тестом LayerDependencyTests.Host_must_not_parse_or_compile_dashspec."
date: 2026-11-23
tags: [dashspec, adr, compiler, plugin, stdlib, presentation, ast, architecture]
language: ru-RU
relates:
  - DASHSPEC-ADR-0076
  - DASHSPEC-ADR-0103
  - DASHSPEC-ADR-0130
  - DASHSPEC-ADR-0131
  - DASHSPEC-ADR-0132
---

# DASHSPEC-ADR-0135: Плагин компилятора языка (`language_ds` / `IReportCompiler`), стандартные презентационные пресеты в `stdlib` и отказ от нетипизированных дефолтов

## Context

1. **Прямые вызовы парсера `.ds` из `DashSpec.Host` и `DashSpec.Execution.Compilation`.**
   До настоящего решения `DashSpec.Host`, `ReportSpecBootstrap` и `DashSpecDefaultShell` напрямую вызывали статические фасады `DashSpecParser.Compile`, `CatalogReader`, `HostModuleParser` и `RuntimeModuleParser`. Это связывало хост с конкретным текстовым представлением `.ds` и мешало как подмене фронтенда компиляции (например, на загрузчик предкомпилированных пакетов), так и чистому разделению микроядра и языковых плагинов (ADR-0131).

2. **Скрытые дефолты и строковые списки свойств в C#-коде.**
   Значения по умолчанию для таблиц и диаграмм (`visible_rows`, `size`, `column_size`, `align`, `grid_lines`, `markers`) частично задавались в C#-компонентах (`TypedTableGrid`, `RadzenCardViz`), а допустимые ключи презентаций дублировались строковым массивом `PresentationProperties` в `DashSpecParseOptionsProvider`.

3. **Строковая типизация в AST (`DirectiveClause("use", ...)` и `DashSpecBlockKeyword.Other`).**
   Директива наследования пресетов `use <preset>` и ряд блочных ключевых слов (`when`, `table`, `columns`, `entry`, `import`) обрабатывались через строковые сравнения вместо явных кейсов F# Discriminated Unions.

## Decision

### 1. Контракт `IReportCompiler` и встроенный плагин `language_ds`

В `DashSpec.Compiler.Contract` вводится единый порт компиляции и чтения модулей спецификации **`IReportCompiler`**:

- `Compile(string text, string? specDirectory, DashSpecParseOptions? options)`
- `Parse(string text, DashSpecParseOptions? options)`
- `Validate(string text, string? specDirectory, DashSpecParseOptions? options)`
- `ReadCatalog(string catalogFilePath)` / `ParseCatalog(string text, string? directory)`
- `TryLoadHostConfiguration(string? specDirectory, out HostModuleConfiguration config)`
- `ReadRuntimePath(string text)` / `ParseRuntimeFile(string runtimeFilePath)` / `ResolveRuntimeConfigPath(...)`

Реализация этого контракта вынесена во встроенный плагин **`DashSpecLanguageBuiltinPlugin`** (`id = "language_ds"`), регистрирующий `IReportCompiler` в DI-контейнере.

В `DashSpec.Architecture.Tests` добавлен тест **`LayerDependencyTests.Host_must_not_parse_or_compile_dashspec`**, запрещающий любое прямое обращение к `DashSpecParser`, `CatalogReader`, `HostModuleParser` и `RuntimeModuleParser` из `DashSpec.Host`.

### 2. Авто-обнаружение `stdlib/**/*.ds` из плагинов и каскад презентационных пресетов

1. `DashSpecContributorRegistry` автоматически обнаруживает встроенные ресурсы `stdlib/**/*.ds` во всех зарегистрированных сборках плагинов (по конвенции имени ресурса `.stdlib.`).
2. `DashSpecParseOptionsProvider` извлекает допустимые ключи свойств презентации напрямую из AST (`DashSpecAstNode.PropertyAssignment`) встроенных модулей `stdlib/presentation/*.ds` через `DashSpecSurfaceSyntax.parse` — без зашитых в C# строковых списков.
3. В `stdlib/presentation/` (`namespace DashSpec.Std`) добавлены стандартные табличные пресеты:
   - **`table_compact.ds`**: `size = content`, `column_size = content`, `align = left`, `visible_rows = 8`, `page_size = 0`, `frozen_columns = 0`, `grouping = off`.
   - **`table_detail.ds`**: `size = content`, `column_size = content`, `align = left`, `visible_rows = 15`, `page_size = 15`, `frozen_columns = 0`, `grouping = off`.
4. Прикладные презентации каталога наследуют базовые пресеты через `use table_compact` / `use table_detail` и переопределяют только отличия. Каскад разрешения свойств строго детерминирован:
   $$\text{stdlib preset (DashSpec.Std)} \longrightarrow \text{project presentation (.ds)} \longrightarrow \text{diagram columns \{ ... \}}$$

### 3. Строгая типизация AST и доменных свойств таблиц

1. В `DashSpecAstNodes.fs` и `DashSpecBlockKeyword.fs` строковые фолбэки заменены на явные кейсы DU:
   - `ClauseKind.UsePresetClause of presetId: string`
   - `DashSpecBlockKeyword.When | Table | Columns | Entry | ImportBlock`
   - `DashSpecKeyword.Use`
2. Для управления размером и выравниванием таблиц и столбцов введены доменные DU:
   - `TableColumnSize = Content | Header | Cells | Fill`
   - `TableColumnAlign = Left | Center | Right | Justify`
   - `TableSortDirection = Asc | Desc`

## Consequences

- **Полная изоляция хоста от парсера `.ds`:** `DashSpec.Host` работает исключительно через `IReportCompiler`, что открывает прямую дорогу для загрузки предкомпилированных пакетов отчётов (см. `DASHSPEC-ADR-0136`).
- **Единый источник дефолтов в `.ds`:** все презентационные умолчания описаны декларативно на самом языке DashSpec в `stdlib/presentation/*.ds` и видны авторам.
- **Отсутствие строковой магии в F#-ядре:** сопоставление с образцом в компиляторе работает по исчерпывающим DU без сравнений сырых строк.
