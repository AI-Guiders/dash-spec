type: Note
id: DASHSPEC-ADR-0120-MAP
title: "ADR-0120 имплементация — карта файлов и порядок работ"
status: "living"
date: 2026-11-07
tags: [dashspec, adr-0120, filters, dashflow, implementation]
language: ru
relates:
  - DASHSPEC-ADR-0120

---

# Карта имплементации ADR-0120 (filters as flow nodes)

## Точки входа (разведка 2026-11-07)

| Слой | Файл | Что там |
|---|---|---|
| IR узлов | `src/DashSpec.Modeling.Parse/DataFlow/DashflowTypes.fs` | `DashflowSourceDef` (порты, row type), `DashflowTransformerDef` (Inputs/Outputs/Steps), `FlowLinkDef` (FromPort/ToPort — **опциональны**, парсер `FlowLinkParser.tryReadBracketPortSameLine` возвращает None без `[`; резолвер падает на `DefaultOutputPort` — голые рёбра уже поддержаны), `DashflowModule` |
| IR фильтров | `src/DashSpec.Modeling.Parse/Filter/FilterTypes.fs` | `FilterKind` (Date/Field/Top), `FilterDefinition` (ColumnReference, DefaultExpression, Widget/Density/Grain…) — презентационная часть останется как show, данные уходят в узлы |
| Компиляция запроса | `src/DashSpec.Execution.Compilation/QueryCompiler.cs` | `BuildDistinctFieldSql`, `CanLoadDistinctFieldOptions` — текущая сборка WHERE; сюда ложится source-локальность (param → WHERE) |
| Опции | `src/DashSpec.Execution.Compilation/Bootstrap/ReportFieldOptionsLoader.cs` | бутстрап-distinct по словарю (ADR-0099); в новой модели — transform на входном потоке узла, лениво per-card |
| Сессия значений | `src/DashSpec.Execution.Runtime/FilterState.cs`, `CardQueryFilterState.cs`, `FilterValueTypes.cs` | состояние значений — станет per-instance параметрами узлов |
| Options cache | `IReportFieldOptionsCache` (там же) | кэш-ключ `{runtime}:{connector}:{sql}` → `{узел}:{от потока}:{период}` |

## Порядок работ

1. **IR**: `DashflowParamPortDef { Name; DashType; Default }` + параметрический узел (`DashflowParamNodeDef`); типы скаляров — из 0079.
2. **Скоуп**: резолв по размещению узла (report `data flow` / card `data flow`) — контекст уже знает владельца.
3. **Session**: значения параметров per-instance keyed by узел+скоуп; инертный выбор (stale) — универсально.
4. **Компилятор**: source-локальность — значения param-узлов сворачиваются в WHERE compiled-запроса; линт runtime-before-source.
5. **Опции**: distinct по входному потоку узла, лениво, кэш per-card.
6. **Транспиляция**: legacy `filter/bind` → узлы на чтении (решено OQ1).
7. **Тулбары**: param-презентация (combobox/day), `when .no_data`.
8. **reduce-плагин** — после фильтров.

## Правила работ

- CDP для всего codebase-доступа (не терминальный grep).
- Каждый слой — с юнит-тестами до следующего.
- Прогон LUS-корпуса (localhost:5000) как приёмка каждого слоя.
