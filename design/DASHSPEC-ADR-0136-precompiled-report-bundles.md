---
type: ADR
id: DASHSPEC-ADR-0136
title: "Предкомпилированные пакеты отчётов (Precompiled Report Bundles) и двухрежимная загрузка в IReportSpecBootstrap"
status: "Proposed"
implementation: "Целевая архитектура: CLI-команда dsc build bundle <corpus-dir> (расширение ADR-0117 и ADR-0124), сериализация скомпилированного графа (DashboardDocument + DiagramLibrary + AssembledSlotPlane + SHA-256 манифест исходных модулей) рядом с DashSpec.TypedRows.dll, и fast-path загрузчик PrecompiledReportBundleLoader за портом IReportCompiler / IReportSpecBootstrap."
date: 2026-11-23
tags: [dashspec, adr, precompiled, bundle, dsc, compiler, performance, deployment]
language: ru-RU
relates:
  - DASHSPEC-ADR-0111
  - DASHSPEC-ADR-0117
  - DASHSPEC-ADR-0124
  - DASHSPEC-ADR-0133
  - DASHSPEC-ADR-0135
---

# DASHSPEC-ADR-0136: Предкомпилированные пакеты отчётов (`Precompiled Report Bundles`) и двухрежимная загрузка в `IReportSpecBootstrap`

## Context

1. **Начальная стоимость парсинга и сборки графа `.ds` в рантайме.**
   При открытии или переключении отчёта `ReportSpecBootstrap.LoadFromTextAsync` читает с диска корневой `.ds` и все транзитивно импортируемые модули (`layouts`, `diagrams`, `flows`, `presentations`, `palettes`, `texts`), строит синтаксические деревья `ParseTree`, компилирует `DashboardDocument`, компонует `DiagramLibrary`, прогоняет `SpecResolver.Resolve` и собирает `AssembledSlotPlane` (`SlotPlaneBuilder.Build`).

2. **Половина предкомпиляции уже существует (`DashSpec.TypedRows.dll`).**
   По `DASHSPEC-ADR-0124` команда `dsc build types <corpus-dir>` уже компилирует объявления `module types` в сборку `catalogs/DashSpec.TypedRows.dll` на этапе сборки/публикации. Кроме того, по `DASHSPEC-ADR-0135` весь фронтенд парсинга `.ds` изолирован за контрактом `IReportCompiler` в плагине `language_ds`.

3. **Разные требования Authoring-контура (Dev / Studio) и Production-контура (Viewer / Forge Release).**
   - В контуре зрителя (`/`) опубликованный каталог неизменен между коммитами: повторно парсить десятки `.ds`-файлов на каждом холодном старте или переключении отчёта избыточно.
   - В корпоративных поставках (и при выкладке через Forge) востребован режим, в котором на production-сервер поставляется уже проверенный, предкомпилированный пакет каталога (`DashSpec.TypedRows.dll` + предкомпилированный граф отчётов) с мгновенным стартом (`~0 мс` на загрузку спецификации).
   - При этом в режиме разработки и при публикации из `/studio` (`StudioGitService`) система должна прозрачно пересобирать пакет или переключаться на живую компиляцию через `language_ds`, если файлы `.ds` на диске изменились.

## Decision

### 1. Артефакт предкомпилированного каталога (`DashSpec.ReportBundle`)

Рядом с `catalogs/DashSpec.TypedRows.dll` вводится артефакт предкомпилированного пакета каталога **`catalogs/DashSpec.ReportBundle.bin`** (или `DashSpec.ReportBundle.dll`), создаваемый командой:

```text
dsc build bundle <corpus-dir>
```

(команда `dsc build` по умолчанию выполняет `build types` + `build bundle`).

Пакет содержит:
1. **`BundleManifest`**:
   - версия формата пакета и версия компилятора `dsc`;
   - корневой SHA-256 хеш корпуса (`CorpusContentHash`) и карта `RelativePath -> SHA-256` всех `.ds` и `.sql` файлов, вошедших в сборку;
   - скомпилированный `CatalogDocument` и `HostModuleConfiguration`.
2. **`CompiledReportEntries`** (для каждого `entry` каталога):
   - готовый `DashboardDocument`;
   - скомпонованная и разрешённая `DiagramLibrary` (после `SpecLibraryComposer.Load` и `SpecResolver.Resolve`);
   - предвычисленная плоскость слотов `AssembledSlotPlane` (`SlotPlaneBuilder.Build`);
   - предкомпилированные шаблоны запросов потоков (`CompiledFlowPlan`).

Для бинарной сериализации иммутабельного графа используется `MemoryPack` (уже входящий в зависимости платформы).

### 2. Двухрежимный резолв в `IReportSpecBootstrap` / `IReportCompiler`

При запросе загрузки отчёта `ReportSpecBootstrap` проверяет наличие и актуальность `DashSpec.ReportBundle`:

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                    ReportSpecBootstrap.LoadAsync                        │
│                                                                         │
│  1. Есть ли catalogs/DashSpec.ReportBundle.bin?                         │
│     ├─ ДА:                                                              │
│     │   • В Production (без DevFileWatcher):                            │
│     │       Мгновенная десериализация (Document, Library, SlotPlane)    │
│     │       из памяти/кеша бандла за < 1 мс.                            │
│     │   • В Development / при включённом Studio:                        │
│     │       Сверка SHA-256 манифеста бандла с файлами на диске.         │
│     │       ├─ Совпадает ──► Fast-path из бандла (< 1 мс).              │
│     │       └─ Изменились ─► Live-компиляция через плагин `language_ds` │
│     │                        + фоновое обновление кеша.                 │
│     └─ НЕТ:                                                             │
│         • Live-компиляция через плагин `language_ds` (IReportCompiler). │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3. Интеграция со Studio (`DASHSPEC-ADR-0133`) и инсталлятором

1. **В `StudioAuthorSession` (черновики):** пока автор редактирует черновик в `/studio`, `StudioPreviewCompiler` использует живой `IReportCompiler` (`language_ds`) поверх `DraftOverlay` в памяти.
2. **При публикации в `StudioGitService` (`Publish & Commit`):** после атомарной записи `.ds` на диск и проверки компиляции `StudioGitService` пересобирает `DashSpec.ReportBundle` (и при изменении `module types` — `DashSpec.TypedRows.dll`), после чего вызывает `DevSpecReloadNotifier.Notify()`.
3. **В скрипте сборки дистрибутива (`build-dashspec-setup.ps1`) и пайплайне Forge:** шаг `0) dsc build` генерирует и `DashSpec.TypedRows.dll`, и `DashSpec.ReportBundle.bin`, которые упаковываются прямо в инсталлятор.

## Consequences

- **Мгновенное открытие и переключение отчётов в рантайме:** исключается чтение десятков мелких `.ds`-файлов, лексинг, парсинг и повторный `SlotPlaneBuilder.Build` при каждом открытии сессии.
- **Проверка целостности всего каталога на этапе сборки:** `dsc build bundle` валидирует все `entry` каталога, все `import`, `flow` и соответствие слотов (`ADR-0109` / `ADR-0111`) ещё до выкладки.
- **Полная обратная совместимость с живым редактированием `.ds`:** благодаря сверке SHA-256 манифеста любая ручная правка `.ds` файла в dev-режиме мгновенно переключает хост на живой компилятор `language_ds`.
