---
type: ADR
id: DASHSPEC-ADR-0138
title: "Документирующие комментарии (/// Markdown Doc Comments), i18n через module texts и генератор самодокументируемой спецификации (dsc doc & /help)"
status: "Accepted"
implementation: "В лексере DashSpecLexer выделен TokenKind.DocComment (/// ...), в AST и UnitGrammar добавлено извлечение привязанных к узлам и символам Markdown-комментариев (DashSpecAst.leadingDocs); в конвейер компиляции (DashboardDocument.Documentation / DashSpecDocumentationExtractor) встроена сборка типизированной модели документации с локализацией через module texts (<kind>.<id>.doc и @text(...)); добавлены CLI-команда dsc doc, живой справочник stdlib и каталога в /help и показ документации в Studio (Hover и Completion)."
date: 2026-11-24
tags: [dashspec, adr, documentation, doc-comments, markdown, stdlib, i18n, compiler, studio, cli]
language: ru-RU
relates:
  - DASHSPEC-ADR-0048
  - DASHSPEC-ADR-0098
  - DASHSPEC-ADR-0130
  - DASHSPEC-ADR-0131
  - DASHSPEC-ADR-0134
  - DASHSPEC-ADR-0135
---

# DASHSPEC-ADR-0138: Документирующие комментарии (`///` Markdown Doc Comments), i18n через `module texts` и генератор самодокументируемой спецификации (`dsc doc` & `/help`)

## Context

1. **Потребность в самодокументируемом языке и каталогах отчётов.**
   DashSpec уже описывает сам себя через встроенные декларативные модули `stdlib` (`stdlib/units/units.ds`, `stdlib/transforms/**/*.ds`, `stdlib/presentation/*.ds`, `stdlib/types/*.ds`, `stdlib/rules/rules.ds`, `stdlib/monitoring/*.ds`), а пользовательские каталоги состоят из модульных `.ds`-файлов (`types`, `flow`, `diagram`, `layout`, `section`, `dashboard`). Однако комментарии `//` и `/* ... */` до сих пор оставались безымянным синтаксическим мусором (`trivia`), не попадающим ни в конвейер компиляции, ни в Studio LSP (Hover / Completion), ни в пользовательскую справку `/help`.

2. **Markdown вместо XML и связка с `module texts` для мультиязычности (i18n).**
   XML-разметка (`<summary>`) избыточна для декларативного языка спецификаций: авторы и аналитики пишут документацию на чистом Markdown. При этом в DashSpec уже есть штатный механизм локализации через `module texts` (`language = "ru-RU"` / `"en-US"`), где по ключу цели (`target | label`) переопределяются заголовки и сообщения правил. Документирующие комментарии должны органично поддерживать переводы через `module texts` без дублирования `.ds`-модулей.

## Decision

### 1. Лексер и поверхностное синтаксическое дерево (`TokenKind.DocComment`)

1. В `TokenKind` (`DashSpec.Modeling.Parse.Lexing`) вводится отдельный вид токена **`TokenKind.DocComment`**:
   - В начале строки три слэша `///` (но не `////`) распознаются лексером как `TokenKind.DocComment`.
   - Две косые черты `//` (или четыре и более `////`) остаются обычным `TokenKind.LineComment`.
2. Для грамматики и парсеров блоков `TokenKind.DocComment` остаётся прозрачным (`isTrivia = true`), поэтому документирующие комментарии можно писать над любым объявлением без изменения грамматических правил.
3. В `DashSpecAst` (`DashSpec.Modeling.Parse.Syntax`) функция **`DashSpecAst.leadingDocs (root: DashSpecAstNode)`** обходит дерево контейнеров и собирает непрерывно идущие строки `TokenKind.DocComment` перед каждым семантическим узлом (`ModuleDeclaration`, `BlockDeclaration`, `CardReference`, `TypeFieldDeclaration`, `PropertyAssignment`, `PortDeclaration`, `TableRowDeclaration`), очищая префикс `///` и склеивая строки в чистый Markdown-блок.

### 2. Локализация (i18n) документации через `module texts`

Для любого документируемого символа поддерживаются два способа локализации:
1. **Внешнее переопределение в `module texts`:**
   В таблицах `module texts` (`language = "ru-RU"`, `"en-US"` и т.д.) любая цель вида `<kind>.<id>.doc` или `<id>.doc` (например, `transform.round.doc`, `card.working_set_chart.doc`, `type.TelemetryQueryRow.doc`, `unit.section.doc`) задаёт локализованный Markdown-текст документации для соответствующего языка интерфейса.
2. **Встроенная ссылка `@text(<key>)` в `///`:**
   Если внутри `///` встречается `@text(some.key)`, резолвер документации подставляет значение `some.key` из активного `module texts` для выбранной культуры.

### 3. Встраивание в конвейер компиляции (`DashboardDocument.Documentation`)

1. В `DashSpec.Modeling.Parse.Document` добавляется типизированная модель документации **`DashSpecSymbolDoc`** (`Kind`, `Id`, `Title`, `Namespace`, `Signature`, `Markdown`, `Children : DashSpecSymbolDoc[]`) и экстрактор **`DashSpecDocumentationExtractor`**.
2. При компиляции отчёта (`DocumentModuleParser` / `DashboardComposer`) извлечённая документация модуля и его импортированных юнитов сохраняется прямо в скомпилированном `DashboardDocument.Documentation : IReadOnlyDictionary<string, DashSpecSymbolDoc>`.

### 4. Генератор документации (`DashSpecDocGenerator`), CLI `dsc doc` и живой `/help`

1. **`DashSpecDocGenerator`** (`DashSpec.Execution.Compilation`):
   - `BuildStdlibCatalog(string? language = null)` — собирает полную самодокументируемую модель по всем встроенным ресурсам `stdlib/**/*.ds` (виды модулей языка, встроенные трансформации и их параметры, стандартные пресеты `presentation`, типы, правила компилятора с локализованными текстами из `stdlib/texts/rules.*.ds` и встроенные отчёты).
   - `BuildWorkspaceCatalog(string workspaceDir, string? language = null)` — собирает полную документацию по всем `.ds`-модулям пользовательского каталога.
   - `RenderMarkdown(...)` — рендерит чистый Markdown-документ с оглавлением, таблицами полей/портов/параметров и описаниями из `///`.
2. **CLI `dsc doc [<dir>] [--stdlib] [--out <file.md>] [--lang <ru-RU|en-US>]`:**
   - Генерирует Markdown-справочник по `stdlib` и/или указанному каталогу `.ds` в консоль или файл.
3. **Интерактивная страница `/help` в `DashSpec.Host`:**
   - Вкладка **«Справочник языка (Stdlib)»** и **«Каталог отчётов»** на странице `/help` рендерят документацию динамически на лету из `DashSpecDocGenerator` (через `<RichTextView />` и карточки символов) — справка по языку и по текущему каталогу всегда актуальна и порождается прямо из `.ds`-кода.
4. **Studio LSP (Hover & Completion):**
   - `StudioGraphAndLspBuilder.BuildHoverInfoAsync` и `DashSpecEditorIntelligence.GetCompletions` подтягивают Markdown-документацию (`///`) и сигнатуру символа как для `stdlib` (трансформации, пресеты, виды модулей), так и для пользовательских модулей, типов, потоков, карточек и фильтров.

## Consequences

- **Плюсы:**
  - Язык DashSpec становится полностью самодокументируемым: документация живёт рядом с кодом в `stdlib/**/*.ds` и пользовательских `.ds` файлах.
  - Никакого дублирования между `/help`, CLI `dsc doc` и подсказками в Studio (Monaco Hover / Autocomplete) — один источник правды в AST и конвейере компиляции.
  - Полная поддержка мультиязычности через `module texts` без изменения синтаксиса модулей.
