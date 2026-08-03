# DASHSPEC-ADR-0039: Chart chrome merge (`chrome { use … }`)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-08 |
| **Relates to** | [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) |

## Context

Chart chrome (`height`, `legend`, `y_max`, `scale_value`, `color_mode`, …) жил в трёх местах:

1. inline в `diagram` kind block (legacy),
2. `include presentation "file.dashpresentation"` на уровне `.dashdiagram`,
3. `presentation { use = … }` на уровне `card`.

Неявный merge через `include presentation` на diagram module смешивал **регистрацию пресета** и **подключение** — сложно читать и отлаживать (особенно цепочки parent+override, как `bar_utilization_percent` → `bar_horizontal_320`).

## Decision

### 1. Именованные пресеты — в `.dashpresentation`

```text
@presentation bar_utilization_percent

include presentation "bar-horizontal-320.dashpresentation"

color_mode = single
default = "#60a5fa"
scale_value = percent
y_max = 100
```

Регистрация в library spec-модуля:

```text
!include "presentations/*.dashpresentation"
```

Каждый файл регистрирует preset по `@presentation <id>` (как `@diagram` для diagrams).

### 2. Подключение на diagram module — `chrome { use … }`

```text
@diagram lus_stakeholder_utilization_bar

chrome
  use bar_utilization_percent
end chrome

bar
  category = app_name
  value = utilization_pct
end bar
```

`chrome` на уровне **diagram module** — синоним chart `presentation` (не путать с `card chrome` для `bound_filters`).

Алиасы:

| Синтаксис | Значение |
|-----------|----------|
| `chrome … end chrome` | chart chrome block |
| `include chrome "file"` | `include presentation "file"` |
| `include presentation "file"` | **deprecated**, парсится как раньше |

### 3. Порядок merge (runtime)

Для итогового chart chrome на карточке:

```text
library preset (chrome use / presentation use)
  → inline overrides в chrome/presentation block (diagram module или card)
  → legacy keys в diagram kind block (deprecated)
  → host defaults (percent cap 100 при scale_value=percent)
```

Card override `card override for <diagram_id> presentation …` — по-прежнему поверх diagram module chrome.

### 4. Card level

На `card` блок по-прежнему **`presentation`** (ADR-0007). `chrome` на card — только card shell (`bound_filters`, extension blocks).

## Non-goals

- Переименование `PresentationBlock` в модели (внутреннее имя сохранено).
- Глобальный dashboard-level chart chrome defaults.
- Автоматическая миграция старых spec в CI (ручная миграция diagram modules).

## Consequences

- LUS stakeholder/overview: `!include "presentations/*.dashpresentation"` + `chrome use …` в diagrams.
- `SpecLibraryComposer` загружает module chart chrome presets **до** module diagram definitions.
- Debug: resolved chrome = merge chain из `ChartChromeProperties`.
