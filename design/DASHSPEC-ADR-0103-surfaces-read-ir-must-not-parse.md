# DASHSPEC-ADR-0103: Surfaces read the F# IR; they must not parse

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-16 |
| **Relates to** | [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) · [ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md) · [ADR-0099](DASHSPEC-ADR-0099-surface-blazor-contract-testing.md) · [ADR-0102](DASHSPEC-ADR-0102-one-language.md) · [ADR-0048 model migration map](DASHSPEC-ADR-0048-model-migration-map.md) |

## Context

[ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md) §3 запрещает зависимость веб-поверхности от F#-парсера тестом:

```csharp
Types.InAssembly(Host).ShouldNot().HaveDependencyOn("DashSpec.Modeling.Parse");
```

Барьер ставился, когда разбор языка существовал **и на C#, и на F#**, а `Modeling.Parse` читался как «второй парсер». Смысл барьера — «поверхность не разбирает текст сама», и он верный.

Но после [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) M4–M8 единственный разбор — F#, и **IR живёт в том же проекте `DashSpec.Modeling.Parse`**: `Document.DashboardDocument`, `Card.CardDefinition`, `Layout.PlacementDefinition` и т. д. Формулировка барьера запрещает не разбор, а **чтение IR** — то есть ровно то, к чему ведёт миграция на [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) (модель — только F#, C# — исполнение).

Цена текущей формулировки: поверхность вынуждена держать C#-копию модели (`DashSpec.Core/Model`, 51 тип) только чтобы не «задеть» барьер. Это тот самый второй источник истины, который ADR-0048 объявляет временным.

## Decision

Барьер сохраняется **по смыслу** и уточняется **по форме**:

| Было | Стало |
|------|-------|
| Поверхность не ссылается на `DashSpec.Modeling.Parse` | Поверхность не вызывает **точки разбора и компиляции** F# |

Точки разбора и компиляции, запрещённые поверхностям:

```text
DashSpec.Modeling.Parse.Lexing
DashSpec.Modeling.Parse.Document.DashSpecCompiler
DashSpec.Modeling.Parse.Document.DashboardComposer
DashSpec.Modeling.Parse.Document.DashboardParser
DashSpec.Modeling.Parse.Document.DocumentModuleParser
DashSpec.Modeling.Parse.Document.TabParser
DashSpec.Modeling.Parse.Syntax
```

Разрешено: **читать типы IR** (`DashSpec.Modeling.Parse.Document`, `.Card`, `.Layout`, `.Filter`, `.Diagram`, `.DataSource`, `.DataFlow`, `.Catalog`, `.Host`, `.Tooltip`, `.Palette`, `.Presentation`, `.Types`, `.Import`) — это SSOT модели.

Разбор и компиляция по-прежнему доступны поверхностям только через фасады `DashSpec.Execution.Core` (`Execution.Parsing.DashSpecParser`) и `DashSpec.Execution.Compilation` (`IReportCompiler`), то есть через конвейер платформы, а не напрямую.

## Consequences

- Барьер перестаёт требовать C#-копию модели: миграция [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) волнами становится возможной.
- Правило остаётся проверяемым сборкой (NetArchTest), но проверяет **поведение**, а не сборку.
- Новые точки разбора, добавленные в `Modeling.Parse`, должны попадать в запрещённый список — это правило обслуживания теста.
- `DashSpec.Surface.Blazor` попадает под то же правило (сейчас оно только для `DashSpec.Host`); распространим, когда поверхность начнёт читать IR напрямую.

## Non-goals

- Разрешать поверхностям вызывать `Tokenize`, `parse`, `compile` напрямую.
- Переносить IR из `Modeling.Parse` в отдельную сборку `Modeling.Model` (вариант без изменения барьера). Отдельная сборка — возможный follow-up, но не обязательное условие: см. карту миграции ADR-0048.

## References

- `tests/DashSpec.Architecture.Tests/LayerDependencyTests.cs` — реализация правила.
- [ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md) — остальные барьеры без изменений.
