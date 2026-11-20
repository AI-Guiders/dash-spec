---
type: ADR
id: DASHSPEC-ADR-0133
title: "DashSpec Studio как вертикальный плагин хоста с изолированными авторскими черновиками (Draft Overlay) и Git-публикацией"
status: "Accepted"
implementation: "Реализован в plugins/DashSpec.Plugin.Studio (id = studio): StudioWorkspaceService (индексация .ds/.sql модулей + Presence), StudioAuthorSession (изолированный In-Memory Draft Overlay на автора + DashSpec.CodeCenter сессии + оптимистическая блокировка по SHA-256), StudioPreviewCompiler (валидация и резолв черновика без записи на диск), StudioGitService (git status/diff/commit + атомарная публикация в Published Catalog с триггером DevSpecReloadNotifier), страница /studio (Explorer + Code/Structure/Wiring & SQL + Live Preview + Diff & Publish). Старый WPF-плагин DashSpec.CodeCenter.Plugin удалён."
date: 2026-11-19
tags: [dashspec, adr, studio, authoring, codecenter, git, concurrency, plugin]
language: ru-RU
relates:
  - DASHSPEC-ADR-0111
  - DASHSPEC-ADR-0112
  - DASHSPEC-ADR-0117
  - DASHSPEC-ADR-0130
  - DASHSPEC-ADR-0132
---

# DASHSPEC-ADR-0133: DashSpec Studio как вертикальный плагин хоста с изолированными авторскими черновиками и Git-публикацией

## Context

1. **Отказ от внешней WPF-оболочки (`DashSpec.CodeCenter.Plugin`).**
   Исторически в репозитории `dash-spec` находился проект `src/DashSpec.CodeCenter.Plugin` (`net11.0-windows`, `UseWPF=true`), который зависел от внешнего репозитория `guiders-wpf` и предоставлял три упрощённые проекции на базовых контролах WPF (`TextBlock`, `ListView`, `WrapPanel`). Он не умел рендерить настоящие карточки, графики ECharts, матрицу, фильтры и `command_bar`, а также нарушал автономность open-source репозитория `dash-spec`.

2. **Один сервер — несколько ролей и одновременных авторов.**
   В целевой топологии развёртывания работает один сервер `DashSpec.Host`, на который по сети заходят пользователи с разных машин:
   - **Зрители (Viewers)** работают с опубликованным каталогом отчётов на маршруте `/`.
   - **Несколько авторов (Authors)** одновременно открывают Studio (`/studio`) с разных машин для редактирования спецификаций `.ds` и `.sql` того же каталога.

3. **Риски прямой записи в рабочую папку сервера.**
   Если бы авторы правили файлы напрямую в рабочей директории хоста (`DashSpecHostContext.Catalog.FullPath`):
   - промежуточное сохранение синтаксически неполного `.ds`-файла ломало бы рабочий дашборд у всех зрителей на `/`;
   - два автора, открывшие один модуль с разных машин, молча перезаписывали бы правки друг друга;
   - `git status` и `git commit` в общей директории смешивали бы незавершённые правки разных людей.

## Decision

### 1. Удаление `DashSpec.CodeCenter.Plugin` и создание вертикального плагина `plugins/DashSpec.Plugin.Studio`

- Проект `src/DashSpec.CodeCenter.Plugin` (и зависимость от `guiders-wpf`) удаляется из `DashSpec.slnx`.
- Языковое ядро `src/DashSpec.CodeCenter` (F#: `DashSpecConceptGraphBuilder`, `DashSpecDocumentLanguageProfile`, `DashSpecCompletion`, `DashSpecStructuralPlanner`, `DashSpecDiagnosticRules`) и `src/DashSpec.Modeling.Language.Adapters.DashSpec` (`DashSpecSymbolIndex`, `DashSpecLanguageBackend`) сохраняются и становятся движком нового плагина **`plugins/DashSpec.Plugin.Studio`** (`id = "studio"`).
- `DashSpec.Plugin.Studio` реализует `IDashSpecPlugin` и `IDashSpecEndpointContributor`, автоматически регистрирует свою сборку в Blazor Router (`AdditionalAssemblies`) для маршрута `/studio` и интегрируется с `command_bar` (`show host studio`).

### 2. Двухконтурная модель на одном сервере: `Published Catalog` vs `Authoring Draft Overlay`

На сервере `DashSpec.Host` строго разделяются два контура:

```text
┌───────────────────────────────────────────────────────────────────────────┐
│                              DashSpec.Host                                │
│                                                                           │
│  [ Контур 1: Published Runtime (/) ]                                      │
│    Читает только опубликованные файлы на диске (Git Working Tree).        │
│    Зрители никогда не видят незавершённые черновики авторов.              │
│                                                                           │
│  [ Контур 2: Studio Authoring Sandboxes (/studio) ]                       │
│    Автор A (машина 1) ──► StudioAuthorSession(A)                          │
│      • BaseSnapshot (SHA-256 файлов на момент старта правки)              │
│      • DraftOverlay: Map<RelativePath, DraftDocument>                     │
│      • CodeCenter DocumentSession + ConceptGraph + Diagnostics            │
│      • Изолированная компиляция и резолв черновика в памяти               │
│                                                                           │
│    Автор B (машина 2) ──► StudioAuthorSession(B)                          │
│      • Свой независимый DraftOverlay                                      │
│                                                                           │
│  [ StudioPresenceTracker (Singleton) ]                                    │
│    Показывает всем авторам, кто какой файл сейчас смотрит или правит.     │
│                                                                           │
│  [ Atomic Publish & Git Gate ]                                            │
│    1. Валидация черновика компилятором (0 ошибок)                         │
│    2. Проверка SHA-256 базовых версий файлов (Optimistic Concurrency)     │
│    3. Атомарная запись на диск + опциональный `git commit` с именем автора│
│    4. Вызов DevSpecReloadNotifier.Notify() → обновление контура зрителей  │
└───────────────────────────────────────────────────────────────────────────┘
```

### 3. Три уровня изоляции и совместной работы

1. **In-Memory Draft Overlay (`StudioAuthorSession`):**
   - Каждый автор в `/studio` работает в своей изолированной сессии (`StudioAuthorSession`), идентифицируемой по имени/ключу автора.
   - Правки текста `.ds` (или структурные правки через форму `DashSpecStructuralPlanner`) меняют только `DraftOverlay` этой сессии в памяти.
   - На каждом изменении `DashSpec.CodeCenter` мгновенно перестраивает `DashSpecConceptGraph`, проверяет диагностические правила и инварианты, а `StudioPreviewCompiler` валидирует и резолвит спецификацию с учётом оверлея без записи на диск.

2. **Presence и оптимистическая блокировка (`StudioPresenceTracker` + `BaseContentHash`):**
   - При открытии или редактировании файла в `StudioAuthorSession` фиксируется `BaseContentHash` (SHA-256 содержимого файла на диске в момент начала редактирования).
   - `StudioPresenceTracker` транслирует в дерево файлов бейджи присутствия: какие файлы сейчас открыты или изменены в черновиках других авторов.
   - Если Автор А опубликовал файл `X.ds`, пока Автор Б держал его в своём черновике, у Автора Б `BaseContentHash != DiskContentHash`: Studio помечает файл статусом `Conflict (Upstream Changed)`, показывает diff между диском и черновиком и блокирует слепую перезапись до явного разрешения (`Accept Disk`, `Keep Draft & Rebase`).

3. **Атомарная публикация и Git (`StudioGitService`):**
   - Публикация черновика (`Publish to Catalog` / `Publish & Git Commit`) выполняется под серверным `SemaphoreSlim`:
     1. Проверяется отсутствие ошибок компиляции в модифицированных модулях.
     2. Проверяется совпадение `BaseContentHash` с текущим состоянием на диске для всех файлов из `DraftOverlay` (если не выбран явный force-rebase после просмотра конфликта).
     3. Файлы записываются на диск атомарно.
     4. Если каталог находится в Git-репозитории и автор выбрал `Commit`, вызывается `git add <files>` и `git commit --author="Author <author@studio>" -m "<message>"`.
     5. Вызывается `DevSpecReloadNotifier.Notify()`, чтобы зрители на `/` получили обновлённый каталог.

### 4. Состав рабочих проекций `/studio`

В интерфейсе `/studio` автору доступны четыре синхронизированные проекции:
- **Explorer & Presence (слева):** логическое дерево модулей каталога (`catalog`, `report`, `layout`, `dashflow`, `sources`, `theme`, `chrome`, `commands`) и файлов `.ds`/`.sql`, фильтрация, индикаторы `Draft`, `Git Modified`, `Conflict` и бейджи других активных авторов.
- **Code & Diagnostics (`DashSpec.CodeCenter`):** редактор `.ds` с номерами строк, мгновенной диагностикой компилятора (`DashSpecDiagnosticService` + `DashSpecDiagnosticRules`), подсказками автодополнения (`DashSpecCompletion`) и быстрым переходом к определениям символов (`DashSpecSymbolIndex`).
- **Structure & Outline (`DashSpecConceptGraph`):** иерархическое дерево узлов AST открытого модуля (секции, карточки, фильтры, раскладки, таблицы) с навигацией и быстрой вставкой шаблонных конструктов Tier 1/2/3.
- **Resolved Cards, Wiring & Live Preview:** таблица разрешённых карточек отчёта (`ResolvedSpecExport` с источниками, фильтрами, диаграммами и параметрами), граф связей модулей и встроенное живое превью дашборда.
- **Diff & Git Bar:** построчный diff между черновиком автора и опубликованным файлом на диске, а также `git status` / `git log` / `git diff` репозитория каталога.

## Consequences

### Положительные
- **Безопасность прода/зрителей:** зрители на `/` полностью защищены от незавершённых правок авторов.
- **Безопасная многопользовательская работа с разных машин:** авторы не затирают файлы друг друга благодаря изолированным `StudioAuthorSession`, индикаторам Presence и проверке `BaseContentHash` при публикации.
- **Чистота репозитория `dash-spec`:** внешняя зависимость от `guiders-wpf` устранена; всё F#-ядро `DashSpec.CodeCenter` и `DashSpec.Modeling.Language.Adapters.DashSpec` работает внутри родного веб-стека.

### Ограничения
- Черновики `StudioAuthorSession` хранятся в памяти процесса `DashSpec.Host` (с возможностью сброса в локальный `.studio-drafts/` при необходимости переживать рестарт процесса); финальным источником истины остаётся опубликованный Git-каталог на диске.
